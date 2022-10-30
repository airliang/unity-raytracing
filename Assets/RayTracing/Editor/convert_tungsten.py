# -*- coding:utf-8 -*-
#how to use this convert tool:
#This tools is for converting tunsten scene to my raytracing scene
#python [path of the input file] [output file]
#example: 
#cmd: cd D:\github\liangairan-s-rendering\Assets\RayTracing\Editor
#python convert_tungsten.py D:\github\tungsten\data\example-scenes\bathroom2\scene.json D:\github\liangairan-s-rendering\Assets\RayTracing\ExampleScenes\bathroom2\bathroom2


import json
from pathlib import Path
from posixpath import abspath
from sys import argv
import os
import glm

g_textures = []
#g_lights = []
g_envLight = []

table = {
    "gamma" : 0,
    "filmic" : 1,
    "reinhard" : 2,
    "linear" : 3,
}

material_table = {
    "dielectric" : 4,
    "rough_plastic" : 5,
    "lambert" : 0,
    "rough_conductor" : 2,
    "rough_dielectric" : 4,
    "plastic" : 5,
    "oren_nayar" : 0,
    "thinsheet" : 0,
    "mirror" : 3,
    "transparency" : 4,
    "null" : 0
}
    

def convert_vec(value, dim=3):
    if type(value) == str:
        return value
    if type(value) != list:
        ret = []
        for i in range(0, dim):
            ret.append(value)
        return ret
    assert len(value) == dim
    return value


def convert_material(mat_input):
    materialType = mat_input["type"]
    albedo = mat_input["albedo"]
    albedoTexture = None
    baseColor = [1,1,1]
    specularColor = [0.04, 0.04, 0.04]
    roughness = 0
    if 'roughness' in mat_input:
        roughness = mat_input["roughness"]
    k = convert_vec(mat_input.get("k", 0), 3)
    eta = convert_vec(mat_input.get("ior", 0), 3)
    metal = None
    
    mat_type_number = material_table[materialType]
    print("converting material type is:", materialType, " reference number is:", mat_type_number)
    if 'material' in mat_input:
        metal = mat_input["material"]
    if type(albedo) == str:
        albedoTexture = convert_vec(mat_input.get("albedo", 1), 3)
    else :
        baseColor = convert_vec(mat_input.get("albedo", 1), 3)
    ret = {
        "type" : mat_type_number,
        "name" : mat_input["name"],
        "assetPath" : "",
        "shaderName" : "RayTracing/Uber",
        "baseColor" : {
            "x" : baseColor[0],
            "y" : baseColor[1],
            "z" : baseColor[2]
        },
        "transmission" : {
            "x" : baseColor[0],
            "y" : baseColor[1],
            "z" : baseColor[2]
        },
        "specular" : 
        {
            "x" : specularColor[0],
            "y" : specularColor[1],
            "z" : specularColor[2]
        },
        "albedoTexture" : albedoTexture,
        "normalTexture" : None,
        "fresnel" : 0,
        "roughnessU" : roughness,
        "roughnessV" : roughness,
        "K" : {
            "x" : k[0],
            "y" : k[1],
            "z" : k[2]
        },
        "eta": {
            "x": eta[0],
            "y": eta[1],
            "z": eta[2]
        },
        "emission": {
            "x": 0,
            "y": 0,
            "z": 0
        },
        "metal" : metal
    }
    return ret

def convert_materials(scene_input):
    mat_inputs = scene_input["bsdfs"]
    mat_outputs = []
    for mat_input in mat_inputs:
        mat_output = convert_material(mat_input)
        #elif mat_type != "null":
        #    mat_output = convert_disney(mat_input)

        if mat_output:
            mat_outputs.append(mat_output)
            
    return mat_outputs

def get_emission(shape):
    if "emission" in shape:
        return convert_vec(shape["emission"], 3)
    else:
        return [0, 0, 0]

def get_power(shape):
    if "power" in shape:
        return shape["power"]
    else:
        return 0


def convert_envmap(shape_input, shape_output):
    assert shape_output is None
    
def rotateZXY(R):
    return glm.rotate(R.y, (0, 1, 0)) * glm.rotate(R.x, (1, 0, 0)) * glm.rotate(R.z, (0, 0, 1)) 

def rotateXYZ(R):
    return glm.rotate(R.z, (0, 0, 1)) * glm.rotate(R.y, (0, 1, 0)) * glm.rotate(R.x, (1, 0, 0))


def convert_trs(S, R, T):
    return glm.translate(T) * rotateXYZ(R) * glm.scale(S)

def decompose_matrix(M, pos, rot, scale):
    pos = M[3]
    scale.x = glm.length(glm.vec3(M[0]))
    scale.y = glm.length(glm.vec3(M[1]))
    scale.z = glm.length(glm.vec3(M[2]))
    rotation_mat = glm.mat3(
        glm.vec3(M[0]) / scale[0],
        glm.vec3(M[1]) / scale[1],
        glm.vec3(M[2]) / scale[2],
    )
    rotation_q = glm.quat_cast(rotation_mat)
    euler = glm.eulerAngles(rotation_q)
    rot.x = glm.degrees(euler.x)
    rot.y = glm.degrees(euler.y)
    rot.z = glm.degrees(euler.z)

def convert_entity(shape_input, shape_type, index):
    transform = shape_input["transform"]
    position = [0, 0, 0]
    scale = [1, 1, 1]
    rotation = [0, 0, 0]
    if 'position' in transform:
        position = transform["position"]
    
    if 'scale' in transform:
        scale = transform["scale"]

    if isinstance(scale, float):
        scale = [scale, scale, scale]

    if 'rotation' in transform:
        rotation = transform["rotation"]

    bsdf = shape_input["bsdf"]
    bsdf = None if type(bsdf) == dict else bsdf
    
    M = convert_trs(glm.vec3(scale), glm.vec3(rotation), glm.vec3(position))
    #skew = glm.vec3(0, 0, 0)
    #perspective = glm.vec4(0, 0, 0, 1)
    #scale_t = glm.vec3(1, 1, 1)
    #rotation_t = glm.quat(0, 0, 0, 1)
    #position = glm.vec3(0, 0, 0)
    #glm.decompose(M, scale_t, rotation_t, position, skew, perspective)
    #rotationM = glm.mat4(rotation_t)
    #euler = glm.degrees(glm.eulerAngles(rotation_t))
    #print("euler angle is:", euler)
    

    emission = get_emission(shape_input)
    power = get_power(shape_input)
    meshcontent = None
    if shape_type == "mesh":
        fn = shape_input["file"]
        fn = fn[:-4] + ".obj"
        meshcontent = fn

    ret = {
        
        "name" : "entity_" + str(index) + "_" + str(bsdf),
        "position" : {
            "x" : position[0],
            "y" : position[1],
            "z" : -position[2]
        },
        "scale" : {
            "x" : scale[0],
            "y" : scale[1],
            "z" : scale[2]
        },
        "rotation" : {
            "x" : rotation[0],
            "y" : rotation[1] + 180,
            "z" : rotation[2]
        },
        "meshType" : shape_type,
        "mesh" : meshcontent,
        "material" : bsdf,
        "emission" : {
            "x" : emission[0],
            "y" : emission[1],
            "z" : emission[2]
        },
        "power" : power
    }
    return ret


def convert_entities(scene_input):
    entity_outputs = []
    shape_inputs = scene_input["primitives"]
    for i, shape_input in enumerate(shape_inputs):
        shape_type = shape_input["type"]
        entity = convert_entity(shape_input, shape_type, i)
        entity_outputs.append(entity)
    return entity_outputs


def convert_camera(scene_input):
    camera_input = scene_input["camera"]
    transform = camera_input["transform"]
    position = transform["position"]
    lookat = transform["look_at"]
    up = transform["up"]

    ret = {
        "position" : {
            "x" : position[0],
            "y" : position[1],
            "z" : -position[2]
        },
        "rotation" : {
            "x" : 0,
            "y" : 0,
            "z" : 0
        },
        "fov" : camera_input["fov"],
        "near" : 0.3,
        "far" : 100,
        "useLookAt" : True,
        "lookAt" : {
            "x" : lookat[0],
            "y" : lookat[1],
            "z" : -lookat[2]
        },
        "up" : {
            "x" : up[0],
            "y" : up[1],
            "z" : up[2]
        }
    }
    return ret

def convert_renderer(scene_input):
    renderer = scene_input["renderer"]
    spp = renderer["spp"]
    integrator = scene_input["integrator"]
    max_bounces = integrator["max_bounces"]
    camera_input = scene_input["camera"]
    #tonemap = camera_input["tonemap"]
    envmap_enable = False
    if 'envmap' in g_envLight:
        envmap_enable = True
    ret = {
        "raytracingData" : {
            "SamplesPerPixel" : spp,
            "MaxDepth" : max_bounces,
            "HDR" : table[camera_input.get("tonemap", "gamma")],
            "_EnviromentMapEnable" : envmap_enable
        }
    }
    return ret

def convert_integrator(scene_input):
    integrator = scene_input["integrator"]
    ret = {
        "type" : "PT",
        "param" : {
			"min_depth" : integrator["min_bounces"],
			"max_depth" : integrator["max_bounces"],
			"rr_threshold" : 1
		}
    }
    return ret


def convert_output_config(scene_input):
    renderer = scene_input["renderer"]
    camera = scene_input["camera"]
    ret = {
        "fn" : renderer.get("output_file", "scene.png"),
        "dispatch_num" : renderer.get("spp", 0),
        "tone_map" : table[camera.get("tonemap", "filmic")]
    }
    return ret

def write_scene(scene_output, filepath):
    with open(filepath, "w") as outputfile:
        json.dump(scene_output, outputfile, indent=4)
    abspath = os.path.join(os.getcwd(), filepath)

def main():
    fn = argv[1]
    if fn == None:
        print('convert file is None, ')
        return

    fn_output = argv[2]
    if len(fn) == 0:
        return

    if len(fn_output) == 0:
        fn_output = "liangairan_scene"
    print('converting file:' + fn)

    parent = os.path.dirname(fn)
    output_fn = os.path.join(parent, fn_output + ".json")
    with open(fn) as file:
        scene_input = json.load(file)
        
    scene_output = {
        "materials" : convert_materials(scene_input),
        "entities" : convert_entities(scene_input),
        "envLight" : g_envLight,
        "jsonCamera" : convert_camera(scene_input),
        "renderer" : convert_renderer(scene_input),
    }
    write_scene(scene_output, output_fn)
    print('convert finish!')


if __name__ == "__main__":
    main()