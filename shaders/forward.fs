#version 410

const int SHADING_WIREFRAME = 1;
const int SHADING_PLANE = 2;
const int SHADING_SOLID = 3;
const int SHADING_NORMAL = 4;
const int SHADING_DEPTH = 5;
const int SHADING_UV= 6;
const int SHADING_INDEX = 7;
const int SHADING_RENDERED = 8;
const int SHADING_CUSTOM = 9;

uniform sampler2D texture_diffuse;
uniform sampler2D texture_metallic;
uniform sampler2D texture_roughness;
struct Material {
  vec4 diffuse;
  float roughness;
  float metallic;
  int type;
};

uniform Material material;
uniform vec3 viewPos;
struct Light {
  vec3 Position;
  vec3 Normal;
  vec4 Color;
  float intensity;
  float Linear;
  float Quadratic;
};

struct Shading{
  int type;
  vec3 studio_light_normal;
  vec3 studio_light_specular;
  float studio_light_shininess;
  vec3 wireframe_color;
  float ambient;
};
uniform Shading shading;

const int NR_LIGHTS = 32;
uniform Light lights[NR_LIGHTS];

// IBL
uniform samplerCube irradianceMap;
uniform samplerCube prefilterMap;
uniform sampler2D brdfLUT;

const float PI = 3.14159265359;

in vec2 Frag_uv;
in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoords;
in vec3 vertexColor;
in vec4 vert_index;

struct CuttingPlane{
  vec3 norm;
  vec3 pos;
  int  enabled;
};
uniform CuttingPlane plane_clipping;
in float cutting_plane_dist;

out vec4 FragColor;

// ----------------------------------------------------------------------------
//
vec3 getNormalFromMap(){
    vec3 tangentNormal = Normal * 2.0 - 1.0;
    vec3 Q1  = dFdx(FragPos);
    vec3 Q2  = dFdy(FragPos);
    vec2 st1 = dFdx(TexCoords);
    vec2 st2 = dFdy(TexCoords);

    vec3 N   = normalize(Normal);
    vec3 T  = normalize(Q1*st2.t - Q2*st1.t);
    vec3 B  = -normalize(cross(N, T));
    mat3 TBN = mat3(T, B, N);

    return normalize(TBN * tangentNormal);
}

float DistributionGGX(vec3 N, vec3 H, float roughness){
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySchlickGGX(float NdotV, float roughness){
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}
// ----------------------------------------------------------------------------
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness){
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}
vec3 fresnelSchlick(float cosTheta, vec3 F0){ return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);}
vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness){ return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);}   


vec3 calc_light(vec3 light_pos, vec3 light_color, float roughness, vec3 N, vec3 V, vec3 albedo, float metallic, vec3 F0, float attenuation){
  vec3 L = normalize(light_pos - FragPos);
  vec3 H = normalize(V + L);
  float distance = length(light_pos - FragPos);
  // float attenuation = 1.0 / (distance * distance);
  vec3 radiance = light_color * attenuation;

  // Cook-Torrance BRDF
  float NDF = DistributionGGX(N, H, roughness);   
  float G   = GeometrySmith(N, V, L, roughness);    
  vec3 F    = fresnelSchlick(max(dot(H, V), 0.0), F0);        
  
  vec3 numerator    = NDF * G * F;
  float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001; // + 0.0001 to prevent divide by zero
  vec3 specular = numerator / denominator;
  
   // kS is equal to Fresnel
  vec3 kS = F;
  // for energy conservation, the diffuse and specular light can't
  // be above 1.0 (unless the surface emits light); to preserve this
  // relationship the diffuse component (kD) should equal 1.0 - kS.
  vec3 kD = vec3(1.0) - kS;
  // multiply kD by the inverse metalness such that only non-metals 
  // have diffuse lighting, or a linear blend if partly metal (pure metals
  // have no diffuse light).
  kD *= 1.0 - metallic;	                
      
  // scale light by NdotL
  float NdotL = max(dot(N, L), 0.0);

  // add to outgoing radiance Lo
  // note that we already multiplied the BRDF by the Fresnel (kS) so we won't multiply by kS again
  return (kD * albedo / PI + specular) * radiance * NdotL; 
}

vec3 main_lighting(vec3 base_color, float roughness, float metallic, float ao){		
  vec3 viewDir  = normalize(viewPos - FragPos);
  vec3 albedo = pow(base_color, vec3(2.2));
       
  // calculate reflectance at normal incidence; if dia-electric (like plastic) use F0 
  // of 0.04 and if it's a metal, use the albedo color as F0 (metallic workflow)    
  vec3 F0 = vec3(0.04); 
  F0 = mix(F0, albedo, metallic);

  // input lighting data
  vec3 N = getNormalFromMap();
  vec3 V = normalize(viewPos - FragPos);
  vec3 R = reflect(-V, N); 

  // reflectance equation
  vec3 Lo = vec3(0.0);
  for(int i = 0; i < NR_LIGHTS; ++i){
    float distance = length(lights[i].Position - FragPos);
   float attenuation = 1.0 / (1.0 + lights[i].Linear * distance + lights[i].Quadratic * distance * distance);
    //float attenuation = 1.0 / (distance * distance);
    vec3 l = calc_light(lights[i].Position, lights[i].Color.rgb*lights[i].intensity , roughness, N, V, albedo, metallic, F0, attenuation);
    Lo += max(l, 0);
  }
  // return Lo;

  // ambient lighting (we now use IBL as the ambient term)
  vec3 F = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);
  
  vec3 kS = F;
  vec3 kD = 1.0 - kS;
  kD *= 1.0 - metallic;	  
  
  vec3 irradiance = texture(irradianceMap, N).rgb;
  vec3 diffuse      = irradiance * albedo;
  
  // sample both the pre-filter map and the BRDF lut and combine them together as per the Split-Sum approximation to get the IBL specular part.
  const float MAX_REFLECTION_LOD = 4.0;
  vec3 prefilteredColor = textureLod(prefilterMap, R, roughness * MAX_REFLECTION_LOD).rgb;    
  vec2 brdf  = texture(brdfLUT, vec2(max(dot(N, V), 0.0), roughness)).rg;
  vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);

  // Lo += (kD * diffuse + specular) * ao;

  // vec3 color = ambient + Lo;
  vec3 color = Lo;
  
  color = color / (color + vec3(1.0));// HDR tonemapping
  color = pow(color, vec3(1.0/2.2)); // gamma correct
  return color;
}

// jet colormap
vec3 depthmap(float depth){
  vec3 color = vec3(0.0);
  color.r = depth;
  color.g = 1.0 - depth;
  return color;
}

vec3 phone_lighting(vec3 diffuse ){
  vec3 spec = vec3(0.0);
  vec3 n = normalize(Normal);
  vec3 l = normalize(shading.studio_light_normal);
  vec3 e = normalize(viewPos);

  float intensity = max(dot(n,l), 0.0);
  if (intensity > 0.0) {
      vec3 h = normalize(l + e);
      float intSpec = max(dot(h,n), 0.0);
      spec = shading.studio_light_specular * pow(intSpec, shading.studio_light_shininess);
  }

  vec3 colorOut = intensity * diffuse + spec;
  return colorOut;
}


void main(){
  uint type = material.type;
  uint lower= type & uint(0xF);  // 下位4ビットを取り出す
  bool use_vertex_color = lower == 1;
  bool use_normal_color = lower == 2 || shading.type == SHADING_NORMAL;
  bool use_depth_color  = shading.type == SHADING_DEPTH;
  bool use_uv_color     = lower == 3 || shading.type == SHADING_UV;
  bool use_index_color  = lower == 4 || shading.type == SHADING_INDEX;
  bool force_base_color = lower == 6;
  bool vertex_pbr       = lower == 8;

  bool use_diffuse_texture   = (type & uint(1 << 10)) > 0;
  bool use_metallic_texture  = (type & uint(1 << 11)) > 0;
  bool use_roughness_texture = (type & uint(1 << 12)) > 0;

  vec3 diffuse = use_diffuse_texture ?  texture(texture_diffuse, TexCoords).rgb : material.diffuse.rgb;
  float roughness  = use_roughness_texture ? texture(texture_roughness, TexCoords).r : material.roughness;
  float metallic = use_metallic_texture ? texture(texture_metallic, TexCoords).r : material.metallic;

  // FragColor = vec4(diffuse, 1.0);
#ifdef MELCHIOR_PLATFORM_MACOS
  FragColor.rgb = phone_lighting(diffuse);
#else
  FragColor.rgb = main_lighting(diffuse, roughness, metallic, 1.0);
#endif
  FragColor.rgb += vec3(0.1)* shading.ambient;

  FragColor.a = 1.0;
  
  if(shading.type == SHADING_SOLID) FragColor.rgb = phone_lighting(diffuse) + diffuse * shading.ambient;
  if(vertex_pbr) FragColor.rgb = phone_lighting(vertexColor.rgb) + vertexColor.rgb / 3.0;
  else if(force_base_color || shading.type== SHADING_PLANE) FragColor.rgb = diffuse;

  if(use_vertex_color)      FragColor.rgb = vertexColor.rgb;
  else if(use_normal_color) FragColor.rgb = normalize(Normal.xyz)*0.5 + 0.5;
  else if(use_uv_color)     FragColor.rgb = vec3(TexCoords, 0.0);
  else if(use_depth_color)  FragColor.rgb = depthmap(gl_FragCoord.z / gl_FragCoord.w / 100.0);
  else if(use_index_color)  FragColor.rgb = vert_index.rgb;
  
  if(shading.type == SHADING_WIREFRAME) FragColor.rgb = shading.wireframe_color;

  if(plane_clipping.enabled > 0){
    if(abs(cutting_plane_dist) < 0.03){
      FragColor.rgb = vec3(0,1,1);
      FragColor.a = 1.0;
    }else if(cutting_plane_dist > 0){
      FragColor.a = 0.0;
      discard; // このフラグメントを破棄
    }
  }
}
