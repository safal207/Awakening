namespace Probuzhdenie.FreeCity;

internal static class WorldShader
{
    internal const string Vertex = @"#version 330 core
layout(location=0)in vec3 p;layout(location=1)in vec3 c;layout(location=2)in vec3 n;
uniform mat4 model,view,proj,lightMatrix;uniform vec3 col;
out vec3 fC,fN,fWorld;out float fDistance;out vec4 fShadow;
void main(){
    vec4 w=model*vec4(p,1);vec4 eye=view*w;gl_Position=proj*eye;
    fDistance=-eye.z;fC=col.r<-.5?c:col;fN=mat3(model)*n;
    fWorld=w.xyz;fShadow=lightMatrix*w;
}";

    internal const string Fragment = @"#version 330 core
in vec3 fC,fN,fWorld;in float fDistance;in vec4 fShadow;
uniform vec3 amb,light,fogCol,eyePosition;uniform float fogDensity,shadowStrength,daylight;
uniform int worldPass,materialMode;uniform sampler2D brickTexture;uniform sampler2DShadow sunDepth;
out vec4 o;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3-2*f);return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+1),f.x),f.y);}
float visibility(vec3 normal){
    if(shadowStrength<.001)return 1;
    vec3 p=fShadow.xyz/fShadow.w*.5+.5;
    if(p.z<=0 || p.z>=1 || any(lessThan(p.xy,vec2(0))) || any(greaterThan(p.xy,vec2(1))))return 1;
    float bias=max(.00012,.00065*(1-max(0,dot(normal,normalize(light)))));
    float v=0;vec2 texel=vec2(1.0/2048.0);
    for(int x=0;x<2;x++)for(int y=0;y<2;y++)
        v+=texture(sunDepth,vec3(p.xy+(vec2(x,y)-.5)*texel*1.8,p.z-bias));
    float edge=smoothstep(0,.06,min(min(p.x,p.y),min(1-p.x,1-p.y)));
    return mix(1,v*.25,shadowStrength*edge);
}
void main(){
    vec3 n=normalize(fN),l=normalize(light);
    if(worldPass==0){
        float d=max(dot(n,l),0);o=vec4(fC*(amb+(1-amb)*d),1);return;
    }
    vec3 base=pow(max(fC,vec3(0)),vec3(2.2));
    float roughness=.8;
    if(materialMode==1){
        vec2 uv=vec2(abs(n.x)>.5?fWorld.z:fWorld.x,-fWorld.y)/vec2(1.5,1.15);
        base=texture(brickTexture,uv).rgb*pow(max(fC,vec3(0)),vec3(2.2));
    }else if(materialMode==2){
        float detail=(noise(fWorld.xz*65)-.5)*.19*exp(-fDistance*.035);
        base*=.96+detail;
        roughness=.72;
    }else if(materialMode==3){
        vec2 cell=abs(fract(fWorld.xz/1.35)-.5);
        vec2 aa=max(fwidth(fWorld.xz/1.35),vec2(.0001));
        float joint=max(smoothstep(.487-aa.x,.495+aa.x,cell.x),smoothstep(.487-aa.y,.495+aa.y,cell.y));
        if(n.y>.5)base*=mix(1,.66,joint);
        base*=.95+noise(fWorld.xz*18)*.08;
    }else if(materialMode==4){
        vec3 viewDir=normalize(eyePosition-fWorld);
        float fresnel=pow(1-max(dot(n,viewDir),0),5);
        float panes=.88+noise(floor(fWorld.xy*vec2(.1,.34)))*.14;
        vec3 reflection=mix(vec3(.19,.25,.27),pow(fogCol,vec3(2.2)),.5+.5*reflect(-viewDir,n).y);
        base=mix(base*panes,reflection,.32+fresnel*.58);
        roughness=.18;
    }else if(materialMode==6){
        vec2 face=vec2(abs(n.x)>.5?fWorld.z:fWorld.x,fWorld.y);
        float joint=smoothstep(.47,.5,abs(fract(face.y/.65)-.5));
        base*=mix(1,.86,joint)*(.95+noise(face*14)*.06);
    }
    if(materialMode==7){o=vec4(fC,1);return;}
    float visible=visibility(n),ndl=max(dot(n,l),0);
    vec3 halfDir=normalize(l+normalize(eyePosition-fWorld));
    float spec=pow(max(dot(n,halfDir),0),mix(100,8,roughness))*(1-roughness)*.22;
    vec3 skyLight=mix(vec3(.42,.39,.34),vec3(.73,.84,.95),n.y*.5+.5);
    vec3 ambient=amb*skyLight*.50;
    vec3 sunColor=mix(vec3(.09,.12,.19),vec3(1.18,1.07,.90),daylight);
    vec3 color=base*(ambient+sunColor*ndl*visible*.95)+spec*sunColor*visible;
    float footShade=mix(.83,1,smoothstep(0,1.2,fWorld.y));
    color*=footShade;
    color=pow(max(color,vec3(0)),vec3(1.0/2.2));
    float fog=1-exp(-pow(fDistance*fogDensity,2));
    o=vec4(mix(color,fogCol,clamp(fog,0,1)),1);
}";
}
