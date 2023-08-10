#version 330 core
layout (location = 0) out vec3 gPosition;
layout (location = 1) out vec3 gNormal;
layout (location = 2) out vec4 gAlbedoSpec;
layout (location = 3) out vec3 gParams;

in vec2 TexCoords;
in vec3 FragPos;
in vec3 Normal;
in vec3 vertexColor;

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

void main(){    
  gPosition = FragPos;
  gNormal = normalize(Normal);

  bool use_vertex_color = (material.type & 0x01) > 0;
  bool use_normal_color = (material.type & 0x02) > 0;
  bool use_uv_color     = (material.type & 0x03) > 0;
  bool use_index_color  = (material.type & 0x04) > 0;

  bool use_diffuse_texture   = (material.type & (1 << 10)) > 0;
  bool use_metallic_texture  = (material.type & (1 << 11)) > 0;
  bool use_roughness_texture = (material.type & (1 << 12)) > 0;

  gAlbedoSpec.rgb = use_diffuse_texture ?  texture(texture_diffuse, TexCoords).rgb : material.diffuse.rgb;
  gAlbedoSpec.a = 1;

  gParams.r = use_roughness_texture ? texture(texture_roughness, TexCoords).r : material.roughness;
  gParams.g = use_metallic_texture ? texture(texture_metallic, TexCoords).r : material.metallic;
  gParams.b = material.type & 0xFF;

  if(use_vertex_color){
    gAlbedoSpec.rgb = vertexColor.rgb;
  }else if(use_normal_color){
    vec3 normal_color = normalize(Normal.xyz)*0.5 + 0.5;
    gAlbedoSpec.rgb = normal_color;
  }
}
