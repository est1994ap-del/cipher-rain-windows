// Adapted from CipherRain.metal; retained composition and color mathematics.

struct VertexOut { float4 position : SV_Position; };
struct RainCell { float4 ink; float4 light; };
struct RainEvent { float4 location; float4 parameters; float4 appearance; };
struct RainBootQuad { float4 bounds; float4 uv; float4 light; };
struct RainUniforms {
    float4 viewport; // width, height, cell width, cell height
    float4 grid; // columns, rows, glyph count, event count
    float4 style; // glow, variance, elapsed, boot age (-1 = inactive)
    float4 tailColor;
    float4 headColor;
    float4 title; // age, number of letters, cell travel, seconds after all letters land
    float4 titleOptions; // hide trails, zoom, enabled, continuous rain
    float4 titleLayout; // padded columns, lines, lightning active, storm active
    float4 boot; // quad count, underlying visibility, reserved, reserved
    float4 titleMotion; // original grid columns, rows, zoom, hold seconds
    float4 titleLines; // three lineIndex lengths, arrival travel
};
uint hash(uint x) { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; return x ^ (x >> 16); }
float random(uint x) { return float(hash(x) & 0xffffffu) / 16777215.0; }
float referenceFlashDecay(float age) {
    if (age<0.0 || age>=0.5) return 0.0;
    // Normalized light falloff measured across the author's 195.083–195.583 s
    // flash. A visual calibration, not a claim to recover its original formula.
    const float light[6]={1.0,0.722,0.477,0.216,0.070,0.0};
    float position=age*10.0;
    uint sampleIndex=min(uint(position),4u);
    return lerp(light[sampleIndex],light[sampleIndex+1u],frac(position));
}
VertexOut rainVertex(uint id : SV_VertexID) {
    const float2 positions[3] = {float2(-1,-1),float2(3,-1),float2(-1,3)};
    VertexOut resultVertex; resultVertex.position = float4(positions[id],0,1); return resultVertex;
}
float4 glyphSample(Texture2DArray<float4> atlas, SamplerState smp, float2 local, uint glyph, float2 cell) {
    if (any(local < 0.0) || any(local > 1.0)) return 0;
    // Explicit gradients prevent spurious mip selection at code-cell boundaries.
    return atlas.SampleGrad(smp, float3(local,glyph),float2(1.0 / cell.x,0),float2(0,1.0 / cell.y));
}
float glyphInk(Texture2DArray<float4> atlas, SamplerState smp, float2 local, uint glyph, float2 cell) { return glyphSample(atlas,smp,local,glyph,cell).r; }
// Resolution-independent approximation of the original two soft white quads.
// No original bitmap is distributed, and the sampled rain never moves sideways.
float screenErrorProfile(float2 uv, bool broad) {
    if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
    float edge=broad?0.39:0.13;
    float2 profile=smoothstep(float2(0,0),float2(edge,edge),uv)*smoothstep(float2(0,0),float2(edge,edge),1.0-uv);
    float textureFactor=broad?1.0:0.94+0.06*cos(uv.x*83.0)*cos(uv.y*71.0);
    return profile.x*profile.y*textureFactor;
}
cbuffer Constants : register(b0) { RainUniforms u; };
StructuredBuffer<RainCell> cells : register(t0);
StructuredBuffer<RainEvent> events : register(t1);
StructuredBuffer<uint> dropGlyphs : register(t2);
StructuredBuffer<float4> lightningCells : register(t3);
StructuredBuffer<float> crashMask : register(t4);
StructuredBuffer<RainBootQuad> bootQuads : register(t5);
StructuredBuffer<float4> titleStreams : register(t6);
Texture2DArray<float4> atlas : register(t7);
Texture2DArray<float4> letters : register(t8);
SamplerState smp : register(s0);
float4 rainFragment(VertexOut input) : SV_Target {
    float2 pixel = input.position.xy;
    float2 originalPixel = pixel;
    float2 cell = u.viewport.zw;
    float2 normalized = pixel / u.viewport.xy;
    uint eventCount = uint(u.grid.w);
    bool seamless=u.titleOptions.w>0.5;
    pixel=clamp(pixel,float2(0,0),u.viewport.xy-1);
    uint column=min(uint(pixel.x/cell.x),uint(u.grid.x)-1);
    uint row=min(uint(pixel.y/cell.y),uint(u.grid.y)-1);
    uint index=row*uint(u.grid.x)+column;
    RainCell c=cells[index];
    uint glyph=uint(c.ink.x)%uint(u.grid.z);
    float2 local=frac(pixel/cell); local.y=1.0-local.y;
    float4 glyphValue=glyphSample(atlas,smp,local,glyph,cell);
    if (c.ink.z<1) glyphValue=lerp(glyphSample(atlas,smp,local,uint(c.ink.y)%uint(u.grid.z),cell),glyphValue,c.ink.z);
    float ink=glyphValue.r;
    ink=smoothstep(0.035,0.92,ink);
    float varianceColor=c.light.y*u.style.y;
    float3 tail=clamp(u.tailColor.rgb+float3(varianceColor*0.18,varianceColor,varianceColor*0.22),0.0,1.0);
    float head=clamp(c.light.x,0.0,1.0);
    float3 color=float3(0.001,0.006,0.003)+lerp(tail*0.82,u.headColor.rgb*1.12,head)*ink*c.ink.w;
    float glow=pow(clamp(u.style.x,0.0,1.0),0.85);
    float halo=glyphValue.g*0.95+glyphValue.b*2.8;
    color += tail*halo*glow*c.ink.w*(0.5+0.5*c.light.z)*(0.65+head*1.35);
    float3 unmodifiedRain=color;
    float stormMaskScale=1.0;
    float stormBrightAccent=0.0;
    // Intentional selected effects are separate from the rain's independent lifecycle.
    for (uint e=0; e<eventCount; ++e) {
        RainEvent ev=events[e]; int kind=int(ev.location.w);
        float p=ev.location.z, age=ev.parameters.z, seed=ev.parameters.x;
        float strength=ev.parameters.y*smoothstep(0.0,0.08,p)*(1.0-smoothstep(0.68,1.0,p));
        float signal=0;
        uint frame=uint(age*12);
        if (kind==1) {
            uint phase=uint(age*4.0);
            float flash=1.0-smoothstep(0.72,1.0,frac(age*4.0));
            for(uint bar=0;bar<uint(ev.appearance.y);bar++) {
                float center=floor(random(uint(seed)+bar*991u+phase*673u)*u.grid.y);
                float halfWidth=max(1.0,u.grid.y*(0.035+random(uint(seed)+bar*337u+phase)*0.13));
                signal += (1.0-smoothstep(halfWidth-0.25,halfWidth+0.35,abs(float(row)-center)))*flash*3.4;
            }
        }
        if (kind>=2 && kind<=4) {
            float r=(kind==2?age:max(0.0,age-0.9))*0.55;
            float2 gridDelta=(float2(column,row)+0.5)/u.grid.xy-ev.location.xy;
            float rect=max(abs(gridDelta.x),abs(gridDelta.y)*0.78);
            signal += exp(-pow((rect-r)/max(0.002,cell.x/u.viewport.x*ev.appearance.z),2.0))*4.5*step(0.001,r);
        }
        if (kind==5) {
            uint flags=uint(ev.appearance.x);
            uint count=uint(ev.appearance.y);
            float baseSize=ev.appearance.w;
            float border=ev.appearance.z;
            for(uint b=0;b<count;b++) {
                float2 center=float2(random(uint(seed)+b*37u),random(uint(seed)+b*79u));
                float phase=clamp(p*1.8-float(b)*0.7/max(1.0,float(count)-1.0),0.0,1.0);
                float size=baseSize*((flags&1u)!=0u?lerp(0.55,1.0,random(uint(seed)+b*313u)):1.0);
                size*=(flags&2u)!=0u?1.0:0.5;
                float fade=(flags&2u)!=0u?1.0-phase:1.0;
                float2 q=abs(normalized-center);
                float rect=max(q.x,q.y*0.9);
                signal += exp(-pow((rect-phase*size)/max(0.002,border*size),2.0))*fade*step(0.01,phase)*(1.0-step(1.0,phase))*1.8;
            }
        }
        // Lightning is a sparse precomputed code-cell layer below. Applying the
        // generic whole-event fade here would suppress its sharp path changes.
        if (kind==8) {
            // A broad downward front with irregular, vertically smeared heads.
            float offset=(random(column+uint(seed))-0.5)*0.20;
            float front=-0.2+p*1.5+offset;
            float distance=normalized.y-front;
            signal += exp(-pow(distance/(distance>0?0.075:0.14),2.0))*4.4;
        }
        if (kind==6 && int(column)==int(ev.appearance.x)) {
            uint dropGlyph=dropGlyphs[e*uint(u.grid.y)+row];
            if (dropGlyph!=65535) {
                float phase=fmod(age,4.0)-float(row)/u.grid.y;
                // Recovered original control points: core 0/1/1.5/2,
                // trailing light 1.5/2/3. The stored glyph never moves.
                float core=phase<0?0:(phase<1?phase:(phase<1.5?1:max(0.0,(2.0-phase)*2.0)));
                float trailing=phase<1.5?0:(phase<2?(phase-1.5)*2.0:max(0.0,3.0-phase));
                float4 drop=glyphSample(atlas,smp,local,uint(dropGlyph)%uint(u.grid.z),cell);
                color+=tail*(drop.r*core+(drop.g+drop.b*2.8)*trailing*glow)*ev.parameters.y*1.7;
            }
        }
        if (kind==9) {
            // Stored, drifting rectangles with fractional cell coverage and
            // 4x4 flash weights. No per-frame hash regeneration or shared wrap.
            float mask=u.titleLayout.w>0.5?crashMask[index]:0.0;
            float duration=ev.parameters.w;
            float storm=smoothstep(0.0,0.35,age)*(1.0-smoothstep(duration-2.0,duration,age));
            if (seamless) {
                // Illuminate patches on the original moving field instead of
                // erasing 98.5% of it and appearing to restart the wallpaper.
                color+=unmodifiedRain*max(0.0,0.96-mask)*storm*ev.parameters.y*0.65;
            } else {
                stormMaskScale=lerp(1.0,max(0.04,1.0-mask),min(1.0,storm*ev.parameters.y));
            }
        }
        if (kind==10 || kind==11) {
            if (ev.appearance.x>0.5) {
                // Code flashes have already been subtracted from the mask.
                // Bright flashes additionally illuminate existing glyphs;
                // no featureless full-screen plane hides the code.
                if (kind==11) stormBrightAccent+=ev.appearance.w*ev.parameters.y;
            } else {
                // Older checkpoint flash payloads retain their prior decay.
                color+=unmodifiedRain*referenceFlashDecay(age)*ev.parameters.y*(kind==11?6.0:4.5);
            }
        }
        if (kind==12) {
            int style=int(ev.location.x);
            float2 uv;
            if (style==0) {
                float remaining=1.0-p;
                uv=float2(lerp(remaining*0.25,1.0-remaining*0.25,normalized.x),
                          lerp(0.5+remaining*0.2,1.0-remaining*0.25,normalized.y));
            } else {
                // Original thin flash is six device pixels high. The traveling
                // band has stored height and linear motion, not new row hashes.
                float halfHeight=style==1?3.0/u.viewport.y:ev.appearance.w;
                uv=float2((normalized.x-ev.appearance.x)/(ev.appearance.y-ev.appearance.x),
                          (normalized.y-ev.appearance.z)/(2.0*halfHeight)+0.5);
            }
            float light=screenErrorProfile(uv,style==0)*ev.parameters.y;
            float3 tint=lerp(u.headColor.rgb,float3(0.78,0.91,1.0),0.6);
            // The stored original fade/triangle is already in parameters.y.
            // Continuous mode lights only existing ink/halos, never a flat wash.
            color+=tint*light*(seamless?(ink+halo*glow*0.65)*c.ink.w*3.0:1.0);
        }
        if (signal>0.01) {
            // Crash flashes reveal the existing rain. Filling every empty cell
            // with a new glyph makes the wash become an unrelated solid grid.
            bool solidDeja=kind==1 && (uint(ev.appearance.x)&2u)!=0u;
            bool ownSymbols=kind==5 && (uint(ev.appearance.x)&4u)!=0u;
            uint effectGlyph=ownSymbols?hash(index+uint(seed)+frame*73u)%uint(u.grid.z):glyph;
            float4 effectSample=glyphSample(atlas,smp,local,effectGlyph,cell);
            float coverage=(ownSymbols||solidDeja)?max(c.ink.w,0.92):(kind==7?max(c.ink.w,0.35):c.ink.w);
            float populated=effectSample.r*coverage;
            float3 effectColor=kind==7?float3(0.78,0.91,1.0):lerp(tail,u.headColor.rgb,clamp(signal*0.6,0.0,1.0));
            bool baseColor=kind==1 && (uint(ev.appearance.x)&1u)!=0u;
            if(baseColor) effectColor=tail;
            // The base-color option must not turn green into cyan by clipping
            // the weaker blue component after multiplying a bright band.
            float illumination=baseColor?min(1.0,signal*strength):signal*strength;
            color += effectColor*populated*illumination;
            color += lerp(tail,effectColor,0.4)*(effectSample.g+effectSample.b*2.8)*illumination*glow*coverage*0.65;
        }
    }
    // Inactive triple-buffer slots may contain old lightning. Never glyphValue
    // them unless this frame has an active series; idle uploads cost nothing.
    float4 bolt=u.titleLayout.z>0.5?lightningCells[index]:float4(0,0,0,0);
    if(bolt.y+bolt.w>0.001) {
        float4 a=bolt.y>0.001?glyphSample(atlas,smp,local,uint(bolt.x)%uint(u.grid.z),cell):float4(0,0,0,0);
        float4 b=bolt.w>0.001?glyphSample(atlas,smp,local,uint(bolt.z)%uint(u.grid.z),cell):float4(0,0,0,0);
        float4 lit=a*bolt.y+b*bolt.w;
        // Bright discontinuous symbols with glyph-shaped bloom, not a smooth
        // Gaussian strip. Existing glyphs also pick up the pale lightning tint.
        float existing=glyphValue.r*c.ink.w*(bolt.y+bolt.w)*0.55;
        color+=float3(0.78,0.91,1.0)*(lit.r*2.4+existing);
        color+=float3(0.50,0.73,1.0)*(lit.g*1.2+lit.b*3.2)*glow;
    }
    // Original storm modulation follows its child effects; bright-glyph
    // accents come afterward. Continuous mode always keeps this scale at 1.
    color*=stormMaskScale;
    color+=float3(0.72,0.88,1.0)*(glyphValue.r*2.0+(glyphValue.g+glyphValue.b*2.8)*glow)*c.ink.w*stormBrightAccent;
    if (u.titleOptions.z>0.5 && u.title.x>=0 && u.title.y>0) {
        float2 titleGrid=u.titleMotion.xy;
        float zoom=u.titleMotion.z;
        float2 titlePoint=((originalPixel/u.viewport.xy-0.5)/zoom+0.5)*titleGrid;
        float2 letterSize=u.viewport.xy/titleGrid*zoom;
        // Keep the optional dark presentation separate from the simulation.
        // Continuous mode never changes, zooms, drains or refills the rain.
        float drainFront=u.title.z/max(1.0,u.titleLines.w)*1.3;
        float columnOffset=random(column*997u)*0.17;
        float remaining=smoothstep(drainFront-0.05,drainFront+0.08,normalized.y+columnOffset);
        float returnAge=u.title.w-(u.titleMotion.w+2.75);
        float refill=1.0-smoothstep(returnAge/3.6-0.08,returnAge/3.6+0.08,normalized.y+columnOffset);
        if (!seamless) color *= max(remaining,returnAge>=0?refill:0.0);
        for(uint lineIndex=0;lineIndex<uint(u.titleLayout.y);lineIndex++) {
            float length=u.titleLines[lineIndex];
            float x=titlePoint.x-(floor(titleGrid.x*0.5)-length);
            int titleColumn=int(floor(x*0.5));
            // Two grid columns per letter: one glyph, then one empty column.
            if(x<0 || titleColumn>=int(length) || fmod(x,2.0)>=1.0) continue;
            uint letter=lineIndex*uint(u.titleLayout.x)+uint((u.titleLayout.x-length)*0.5)+uint(titleColumn);
            float4 stream=titleStreams[letter];
            if(stream.z<=0) continue;
            float y=titlePoint.y-stream.x;
            float light=0;
            if(y>=0 && y<1) light=1;
            else if(y<0 && titlePoint.y>=stream.y && stream.w>0) {
                // Repeated copies of the actual incoming letter, not newly
                // randomized rain symbols. Fade-in/hold/resultVertex knots follow the
                // original 0 / .5 / 1.5 / 2 cell-age profile.
                float behind=stream.x-floor(titlePoint.y);
                light=min(1.0,behind/0.5)*min(1.0,max(0.0,(2.0-behind)*2.0));
                // The dimmer body extends to the independently advancing tail.
                light=max(light,0.22*clamp((titlePoint.y-stream.y)/max(1.0,stream.x-stream.y),0.0,1.0));
                light*=stream.w;
            }
            if(light<=0) continue;
            float2 uv=float2(fmod(x,2.0),frac(y));
            float4 titleSample=glyphSample(letters,smp,uv,letter,letterSize);
            color+=float3(0.70,0.80,0.80)*(titleSample.r+titleSample.g*0.28+titleSample.b*glow*0.5)*light*stream.z;
        }
    }
    // Console Start is a one-time CRT light sequence, not a rain reset. Normal
    // continuous mode preserves every underlying pixel; the optional darker
    // mode reveals the same still-evolving rain in the final six phase units.
    if (u.style.w >= 0) {
        color *= u.boot.y;
        float light = 0.0;
        for (uint i=0; i<uint(u.boot.x); ++i) {
            RainBootQuad quad=bootQuads[i];
            float2 at=(normalized-quad.bounds.xy)/(quad.bounds.zw-quad.bounds.xy);
            if (all(at>=0.0) && all(at<=1.0)) {
                float2 uv=lerp(quad.uv.xy,quad.uv.zw,at);
                light += screenErrorProfile(uv,quad.light.y>0.5)*quad.light.x;
            }
        }
        // A full white plane can visually erase the code even without reducing
        // RGB values. Continuous mode lights existing symbols and their halos
        // only; empty cells remain untouched. Dark mode retains the light plane.
        float coverage=seamless?(ink+halo*glow*0.65)*c.ink.w*2.0:1.0;
        color += float3(0.9,0.9,0.9)*light*coverage;
    }
    return float4(clamp(color,0.0,1.0),1);
}
