#version 330 core
layout (lines) in;
layout (triangle_strip, max_vertices = 5) out;

out vec3 fColor;
in Inputs {
   vec3 pos;
   vec3 frag_color;
} gin[2];

void build_line(vec2 from, vec2 to, float width){
  vec2 dir = normalize(to - from);
  vec2 n = vec2(-dir.y, dir.x) * width;

  gl_Position = vec4(from - n, 0.0, 1.0);    EmitVertex();
  gl_Position = vec4(from + n, 0.0, 1.0);    EmitVertex();
  gl_Position = vec4(to - n, 0.0, 1.0);      EmitVertex();
  gl_Position = vec4(to + n, 0.0, 1.0);      EmitVertex();
  gl_Position = vec4(from - n, 0.0, 1.0);    EmitVertex();

  EndPrimitive();
}

void main() {    
  fColor = gin[0].frag_color;
  build_line(gin[0].pos.xy, gin[1].pos.xy, gin[0].pos.z);
}  
