#version 330 core
layout (location = 0) in vec3 position;
layout (location = 3) in vec3 color;
uniform mat4 mvp;
uniform vec2 resolution;
uniform float line_width;

out Inputs {
   vec3 pos;
   vec3 frag_color;
} vert;

void main(){
  vec4 world_pos = mvp * vec4(position, 1.0);
  world_pos.x /= world_pos.w;
  world_pos.y /= world_pos.w;
  vert.frag_color = color;
  vert.pos.xy = world_pos.xy;
  vert.pos.z = line_width / resolution.x;
  gl_Position = world_pos;
}

