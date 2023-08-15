#version 330 core
out vec4 FragColor;
in vec2 TexCoords;

uniform sampler2D gPosition;
uniform sampler2D gNormal;
uniform sampler2D gAlbedoSpec;
uniform sampler2D gParams;

struct Light {
  vec3 Position;
  vec4 Color;
  float intensity;
  float Linear;
  float Quadratic;
};

const int NR_LIGHTS = 32;
uniform Light lights[NR_LIGHTS];
uniform vec3 viewPos;

uniform sampler2D aoMap;

// IBL
uniform samplerCube irradianceMap;
uniform samplerCube prefilterMap;
uniform sampler2D brdfLUT;


const float PI = 3.14159265359;

vec3 Normal;
vec3 WorldPos;

// ----------------------------------------------------------------------------
// Easy trick to get tangent-normals to world-space to keep PBR code simplified.
// Don't worry if you don't get what's going on; you generally want to do normal 
// mapping the usual way for performance anyways; I do plan make a note of this 
// technique somewhere later in the normal mapping tutorial.
vec3 getNormalFromMap(){
    vec3 tangentNormal = texture(gNormal, TexCoords).xyz * 2.0 - 1.0;
    vec3 Q1  = dFdx(WorldPos);
    vec3 Q2  = dFdy(WorldPos);
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
  vec3 L = normalize(light_pos - WorldPos);
  vec3 H = normalize(V + L);
  float distance = length(light_pos - WorldPos);
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

vec3 main_lighting(){		
  vec3 WorldPos = texture(gPosition, TexCoords).rgb;
  vec3 Normal = texture(gNormal, TexCoords).rgb;
  vec3 viewDir  = normalize(viewPos - WorldPos);

  vec3 albedo = pow(texture(gAlbedoSpec, TexCoords).rgb, vec3(2.2));
  float metallic = texture(gParams, TexCoords).r;
  float roughness = texture(gParams, TexCoords).g;
  float ao = texture(aoMap, TexCoords).r;
       
  // calculate reflectance at normal incidence; if dia-electric (like plastic) use F0 
  // of 0.04 and if it's a metal, use the albedo color as F0 (metallic workflow)    
  vec3 F0 = vec3(0.04); 
  F0 = mix(F0, albedo, metallic);

  // input lighting data
  vec3 N = getNormalFromMap();
  vec3 V = normalize(viewPos - WorldPos);
  vec3 R = reflect(-V, N); 

  // reflectance equation
  vec3 Lo = vec3(0.0);
  for(int i = 0; i < NR_LIGHTS; ++i){
    float distance = length(lights[i].Position - WorldPos);
   float attenuation = 1.0 / (1.0 + lights[i].Linear * distance + lights[i].Quadratic * distance * distance);
    //float attenuation = 1.0 / (distance * distance);
    vec3 l = calc_light(lights[i].Position, lights[i].Color.rgb*lights[i].intensity , roughness, N, V, albedo, metallic, F0, attenuation);
    Lo += max(l, 0);
  }
  return Lo;


  // ambient lighting (we now use IBL as the ambient term)
  vec3 F = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);
  
  vec3 kS = F;
  vec3 kD = 1.0 - kS;
  kD *= 1.0 - metallic;	  
  
  vec3 irradiance = texture(irradianceMap, N).rgb;
  vec3 diffuse      = irradiance * albedo;
  
  // sample both the pre-filter map and the BRDF lut and combine them together as per the Split-Sum approximation to get the IBL specular part.
  const float MAX_REFLECTION_LOD = 4.0;
  vec3 prefilteredColor = textureLod(prefilterMap, R,  roughness * MAX_REFLECTION_LOD).rgb;    
  vec2 brdf  = texture(brdfLUT, vec2(max(dot(N, V), 0.0), roughness)).rg;
  vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);

  vec3 ambient = (kD * diffuse + specular) * ao;
  // vec3 color = ambient + Lo;
  vec3 color = Lo;
  
  color = color / (color + vec3(1.0));// HDR tonemapping
  color = pow(color, vec3(1.0/2.2)); // gamma correct
  return color;
}


void main(){             
  // retrieve data from gbuffer
  WorldPos = texture(gPosition, TexCoords).rgb;
  Normal = texture(gNormal, TexCoords).rgb;
  vec3 Diffuse = texture(gAlbedoSpec, TexCoords).rgb;

  vec3 lighting  = Diffuse * 0.3; // hard-coded ambient component
  lighting += main_lighting(); // get lighting using our BRDF function
  FragColor = vec4(lighting, 1.0);
  int type = int(texture(gParams, TexCoords).b * 255.0);
  bool use_vertex_color = (type & 0x01) > 0;
  bool use_normal_color = (type & 0x02) > 0;
  bool use_uv_color     = (type & 0x03) > 0;
  bool use_index_color  = (type & 0x04) > 0;
  bool force_base_color = (type & 0x06) > 0;

  if(use_vertex_color || use_normal_color || use_uv_color || use_index_color || force_base_color){
   FragColor.rgb = Diffuse;
  }
  FragColor.a = 1.0;
}
