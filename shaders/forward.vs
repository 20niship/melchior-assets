#version 330 core
layout (location = 0) in vec3 position;
layout (location = 1) in vec3 norm;
layout (location = 2) in vec2 vuv;
layout (location = 3) in vec3 color;

out vec3 FragPos;
out vec2 TexCoords;
out vec3 Normal;
out vec3 vertexColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 proj;
uniform vec2 uvsize;

void main(){
    vec4 worldPos = model * vec4(position, 1.0);
    FragPos = worldPos.xyz; 
    
    mat3 normalMatrix = transpose(inverse(mat3(model)));
    Normal = normalMatrix * norm;

    // gl_Position = proj* view * worldPos;
    gl_Position = proj * model * vec4(position, 1.0);

    TexCoords = vuv;
    vertexColor = color / 255.0;
}


