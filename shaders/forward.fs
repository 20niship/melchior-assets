#version 450

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

in vec2 Frag_uv;
in vec3 FragPos;
in vec3 Normal;
in vec4 vertColor;
in vec2 TexCoords;
in vec3 vertexColor;

struct CuttingPlane{
  vec3 norm;
  vec3 pos;
  float enabled;
};
uniform CuttingPlane plane_clipping;
in float cutting_plane_dist;

out vec4 FragColor;

void main(){    
  bool use_vertex_color = (material.type & 0x01) > 0;
  bool use_normal_color = (material.type & 0x02) > 0;
  bool use_uv_color     = (material.type & 0x03) > 0;
  bool use_index_color  = (material.type & 0x04) > 0;

  bool use_diffuse_texture   = (material.type & (1 << 10)) > 0;
  bool use_metallic_texture  = (material.type & (1 << 11)) > 0;
  bool use_roughness_texture = (material.type & (1 << 12)) > 0;

  vec3 diffuse = use_diffuse_texture ?  texture(texture_diffuse, TexCoords).rgb : material.diffuse.rgb;
  float roughness  = use_roughness_texture ? texture(texture_roughness, TexCoords).r : material.roughness;
  float metallic = use_metallic_texture ? texture(texture_metallic, TexCoords).r : material.metallic;

  FragColor = vec4(diffuse, 1.0);

  if(use_vertex_color){
    FragColor.rgb = vertexColor.rgb;
  }else if(use_normal_color){
    vec3 normal_color = normalize(Normal.xyz)*0.5 + 0.5;
    FragColor.rgb = normal_color;
  }else if(use_uv_color){
    FragColor.rgb = vec3(TexCoords, 0.0);
  }
}
