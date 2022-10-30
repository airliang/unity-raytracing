using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;

public static class JsonScene
{
    [Serializable]
    public class Material
    {
        public int type = 0;
        public string name;
        public string assetPath;
        public string shaderName;
        public Vector3 baseColor = Vector3.one;
        public Vector3 transmission = Vector3.one;
        public Vector3 specular = Vector3.zero;
        public string albedoTexture;
        public string normalTexture;
        public int fresnel;
        public float roughnessU;
        public float roughnessV;
        public Vector3 K;
        public Vector3 eta = Vector3.one;
        public Vector3 emission = Vector3.zero;
        public string metal;
    }


    [Serializable]
    public class EnvmapLight
    {
        public Vector3 emission = Vector3.zero;
        public string material;
        public string envmap;
    }

    [Serializable]
    public class Transform
    {
        public float m00;
        public float m01;
        public float m02;
        public float m03;
        public float m10;
        public float m11;
        public float m12;
        public float m13;
        public float m20;
        public float m21;
        public float m22;
        public float m23;
        public float m30;
        public float m31;
        public float m32;
        public float m33;

        public float this[int index]
        {
            get
            {
                return index switch
                {
                    0 => m00,
                    1 => m10,
                    2 => m20,
                    3 => m30,
                    4 => m01,
                    5 => m11,
                    6 => m21,
                    7 => m31,
                    8 => m02,
                    9 => m12,
                    10 => m22,
                    11 => m32,
                    12 => m03,
                    13 => m13,
                    14 => m23,
                    15 => m33,
                    _ => throw new IndexOutOfRangeException("Invalid matrix index!"),
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        m00 = value;
                        break;
                    case 1:
                        m10 = value;
                        break;
                    case 2:
                        m20 = value;
                        break;
                    case 3:
                        m30 = value;
                        break;
                    case 4:
                        m01 = value;
                        break;
                    case 5:
                        m11 = value;
                        break;
                    case 6:
                        m21 = value;
                        break;
                    case 7:
                        m31 = value;
                        break;
                    case 8:
                        m02 = value;
                        break;
                    case 9:
                        m12 = value;
                        break;
                    case 10:
                        m22 = value;
                        break;
                    case 11:
                        m32 = value;
                        break;
                    case 12:
                        m03 = value;
                        break;
                    case 13:
                        m13 = value;
                        break;
                    case 14:
                        m23 = value;
                        break;
                    case 15:
                        m33 = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Invalid matrix index!");
                }
            }
        }
    }

    [Serializable]
    public class Entity
    {
        public string name;
        public Vector3 position;
        public Vector3 scale = Vector3.one;
        public Vector3 rotation;
        public string meshType;
        public string mesh;
        public string material;
        public Vector3 emission = Vector3.zero;
        public float power = 0;
    }

    [Serializable]
    public class Camera
    {
        public Vector3 position;
        public Vector3 rotation;
        public float fov;
        public float near;
        public float far;
        public bool useLookAt = false;
        public Vector3 lookAt;
        public Vector3 up = Vector3.up;
    }

    [Serializable]
    public class Renderer
    {
        public RaytracingData raytracingData;
    }

    [Serializable]
    public class Scene
    {
        //public JsonScene.AreaLight[] areaLights;
        public JsonScene.Material[] materials;
        public JsonScene.Entity[] entities;
        public JsonScene.EnvmapLight envLight = new JsonScene.EnvmapLight();
        public JsonScene.Camera jsonCamera = new JsonScene.Camera();
        public JsonScene.Renderer renderer = new JsonScene.Renderer();

        public void ConvertFromUnity(MeshRenderer[] meshRenderers, UnityEngine.Camera camera)
        {
            List<Mesh> sharedMeshes = new List<Mesh>();
            List<UnityEngine.Material> sharedMaterials = new List<UnityEngine.Material>();
            Dictionary<Mesh, List<int>> meshHandlesDict = new Dictionary<Mesh, List<int>>();

            entities = new Entity[meshRenderers.Length];
            jsonCamera.position = camera.transform.position;
            jsonCamera.rotation = camera.transform.eulerAngles;
            jsonCamera.fov = camera.fieldOfView;
            jsonCamera.near = camera.nearClipPlane;
            jsonCamera.far = camera.farClipPlane;

            //extract meshes
            for (int i = 0; i < meshRenderers.Length; ++i)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                //worldMatrices[i] = shapes[i].transform.localToWorldMatrix;

                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter.sharedMesh;

                //if (mesh.normals == null || mesh.normals.Length == 0)
                //    mesh.RecalculateNormals();
                if (!sharedMeshes.Contains(mesh))
                    sharedMeshes.Add(mesh);

                UnityEngine.Material material = meshRenderer.sharedMaterial;
                if (!sharedMaterials.Contains(material))
                {
                    sharedMaterials.Add(material);
                }
            }

            if (sharedMaterials.Count > 0)
            {
                materials = new JsonScene.Material[sharedMaterials.Count];
                for (int i = 0; i < sharedMaterials.Count; ++i)
                {
                    UnityEngine.Material material = sharedMaterials[i];
                    Material jsonMaterial = new Material();
                    if (material.shader.name == "RayTracing/Uber")
                    {
                        jsonMaterial.name = material.name;
                        jsonMaterial.assetPath = AssetDatabase.GetAssetPath(material);
                        jsonMaterial.shaderName = material.shader.name;
                        jsonMaterial.type = material.GetInt("_MaterialType");
                        jsonMaterial.baseColor = material.GetColor("_BaseColor").LinearToVector3();
                        if (material.GetFloat("_UseLinearBaseColor") == 1.0f)
                            jsonMaterial.baseColor = material.GetVector("_BaseColorLinear");
                        jsonMaterial.transmission = material.GetColor("_t").LinearToVector3();
                        jsonMaterial.roughnessU = material.GetFloat("_roughnessU");
                        jsonMaterial.roughnessV = material.GetFloat("_roughnessV");
                        jsonMaterial.eta = material.GetVector("_eta");
                        jsonMaterial.K = material.GetVector("_k");
                        jsonMaterial.fresnel = (int)material.GetFloat("_FresnelType");
                    }
                    else if (material.shader.name == "RayTracing/AreaLight")
                    {
                        jsonMaterial.name = material.name;
                        jsonMaterial.assetPath = AssetDatabase.GetAssetPath(material);
                        jsonMaterial.shaderName = material.shader.name;
                        jsonMaterial.baseColor = material.GetColor("_Emission").LinearToVector3();
                        jsonMaterial.emission = material.GetVector("_Intensity");
                        jsonMaterial.emission = jsonMaterial.emission.Mul(jsonMaterial.baseColor);
                    }
                    materials[i] = jsonMaterial;
                }
            }
            

            //generate entities
            for (int i = 0; i < meshRenderers.Length; ++i)
            {
                entities[i] = new Entity();
                entities[i].name = meshRenderers[i].gameObject.name;
                //entities[i].transform = meshRenderers[i].transform.localToWorldMatrix;
                //entities[i].position = meshRenderers[i].transform.position;
                //entities[i].scale = meshRenderers[i].transform.localScale;
                //entities[i].rotation = meshRenderers[i].transform.eulerAngles;
                MeshFilter meshFilter = meshRenderers[i].GetComponent<MeshFilter>();
                string meshName = meshFilter.sharedMesh.name;
                entities[i].mesh = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                if (entities[i].mesh.Length == 0)
                {
                    entities[i].mesh = AssetDatabase.GetAssetPath(meshFilter.gameObject);
                }
                if (entities[i].mesh.Length == 0)
                {
                    //Transform parent = meshFilter.transform.parent;
                    //if (parent != null)
                    //{
                    //    entities[i].mesh = AssetDatabase.GetAssetPath(parent.gameObject);
                    //}
                    if (meshName == "Plane")
                    {
                        entities[i].meshType = "Plane";
                    }
                    else if (meshName == "Cube")
                    {
                        entities[i].meshType = "Cube";
                    }
                    else if (meshName == "Sphere")
                    {
                        entities[i].meshType = "Sphere";
                    }
                    else if (meshName == "Quad")
                    {
                        entities[i].meshType = "Quad";
                    }
                }

                Light lightComponent = meshRenderers[i].gameObject.GetComponent<Light>();
                if (lightComponent != null && lightComponent.type == LightType.Area)
                {
                    UnityEngine.Material lightMaterial = meshRenderers[i].sharedMaterial;
                    if (lightMaterial != null && lightMaterial.shader.name == "RayTracing/AreaLight")
                    {
                        Color emssionColor = lightMaterial.GetColor("_Emission");
                        Vector3 lightIntensity = lightMaterial.GetVector("_Intensity");
                        entities[i].emission = emssionColor.LinearToVector3().Mul(lightIntensity);
                    }
                }
                else
                {
                    entities[i].material = meshRenderers[i].sharedMaterial.name;
                }
            }

            UnityEngine.Material skyBoxMaterial = RenderSettings.skybox;
            if (skyBoxMaterial != null)
            {
                Texture envmapTexture = null;
                if (skyBoxMaterial.shader.name == "Skybox/Cubemap")
                {
                    envmapTexture = skyBoxMaterial.GetTexture("_Tex");
                }
                else if (skyBoxMaterial.shader.name == "Skybox/Panoramic" || skyBoxMaterial.shader.name == "RayTracing/SkyboxHDR")
                {
                    envmapTexture = skyBoxMaterial.GetTexture("_MainTex");
                }
                if (envmapTexture != null)
                {
                    envLight.envmap = AssetDatabase.GetAssetPath(envmapTexture);
                }
                envLight.material = AssetDatabase.GetAssetPath(skyBoxMaterial);
            }

            jsonCamera.position = camera.transform.position;
            jsonCamera.rotation = camera.transform.eulerAngles;
            jsonCamera.fov = camera.fieldOfView;
            jsonCamera.near = camera.nearClipPlane;
            jsonCamera.far = camera.farClipPlane;

            Raytracing raytracingComponent = camera.GetComponent<Raytracing>();
            renderer.raytracingData = raytracingComponent._RayTracingData;
        }
    }
}
