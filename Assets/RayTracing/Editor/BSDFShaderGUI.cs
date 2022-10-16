using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
public class BSDFShaderGUI : ShaderGUI
{
    public enum BSDFMaterial
    {
        Matte,
        Plastic,
        Metal,
        Mirror,
        Glass,
        Substrate,
    }

    public enum BSDFFresnel
    {
        Dielectric,
        Conductor,
        Schlick,
        NoOp,
    }

    private static class Styles
    {
        public static string materialType = "Material Type";
        public static string fresnelType = "Fresnel Type";
        public static string metalType = "Metals";

        public static readonly string[] materialNames = Enum.GetNames(typeof(BSDFMaterial));
        public static readonly string[] fresnelNames = Enum.GetNames(typeof(BSDFFresnel));
        public static string[] metalNames = MetalData.GetMetalNames();//Enum.GetNames(typeof(MetalData.MetalType));

        public static GUIContent albedoText = new GUIContent("Albedo", "Albedo (RGB) and Transparency (A)");
        public static GUIContent linearAlbeoText = new GUIContent("linear Base Color", "Use linear Base Color instead of Albedo");
        public static GUIContent tilingText = new GUIContent("Tiling", "Tiling");
        //public static GUIContent uvOffsetText = new GUIContent("Offset", "Offset");

        public static GUIContent normalText = new GUIContent("Normal", "Normal Texture");

        public static GUIContent glossySpecularMapText = new GUIContent("Glossy Specular", "Glossy Specular Texture");
        public static GUIContent glossySpecularColorText = new GUIContent("Glossy Specular Color", "Glossy Specular Color");

        public static GUIContent roughnessText = new GUIContent("Roughness", "Roughness");
        public static GUIContent roughnessUText = new GUIContent("Roughness U", "Roughness U");
        public static GUIContent roughnessVText = new GUIContent("Roughness V", "Roughness V");
        public static GUIContent etaText = new GUIContent("Eta", "Refraction Index");
        public static GUIContent metallicAbsorptionText = new GUIContent("Metallic K", "Metallic");
        public static GUIContent transmissionText = new GUIContent("Transmission", "Transmission");

        public static GUIContent materialTypeText = new GUIContent("Material choose", "matte, plastic, metal, glass");
        public static GUIContent fresnelTypeText = new GUIContent("Fresnel choose", "Dielectric, Conductor, Schlick");

        public static string surfaceProperties = "Surface Properties";
        public static string function = "Function";
    }

    protected MaterialEditor materialEditor { get; set; }

    protected MaterialProperty materialTypeProp { get; set; }
    protected MaterialProperty metalTypeProp { get; set; }
    //protected MaterialProperty surfaceTypeProp { get; set; }
    //protected MaterialProperty blendModeProp { get; set; }
    //protected MaterialProperty cullModeProp { get; set; }

    //private MaterialProperty spreadUV;
    //private MaterialProperty worldSpaceUV;
    //private MaterialProperty useTextureArray;
    //private MaterialProperty useTextureAtlas;
    //private MaterialProperty textureMode;

    private MaterialProperty albedoColor;
    private MaterialProperty albedoMap;
    private MaterialProperty useLinearAlbedoColor;
    private MaterialProperty linearAlbedoColor;
    private MaterialProperty normalMap;
    private MaterialProperty glossySpecularMap;
    private MaterialProperty glossySpecularColor;


    private MaterialProperty roughnessU;
    private MaterialProperty roughnessV;
    private MaterialProperty eta;
    private MaterialProperty k;
    private MaterialProperty t;

    private MaterialProperty fresnelTypeProp { get; set; }

    //private MaterialProperty staticBatch;

    private bool m_FirstTimeApply = true;
    private bool useLinearVectorColor = false;
    private int fresnelType = 0;

    private void MaterialChanged(Material material)
    {
        if (material == null)
            throw new ArgumentNullException("material");

        material.shaderKeywords = null;
    }

    public void FindProperties(MaterialProperty[] properties)
    {
        
        materialTypeProp = FindProperty("_MaterialType", properties);
        albedoColor = FindProperty("_BaseColor", properties);
        useLinearAlbedoColor = FindProperty("_UseLinearBaseColor", properties);
        linearAlbedoColor = FindProperty("_BaseColorLinear", properties);
        albedoMap = FindProperty("_MainTex", properties);
        normalMap = FindProperty("_NormalTex", properties);
        glossySpecularMap = FindProperty("_GlossySpecularTex", properties);
        glossySpecularColor = FindProperty("_GlossySpecularColor", properties);
        //albedoTiling = FindProperty("_MainTex_ST", properties);
        //albedoOffset = FindProperty("_UVOffsets", properties);

        roughnessU = FindProperty("_roughnessU", properties);
        roughnessV = FindProperty("_roughnessV", properties);
        eta = FindProperty("_eta", properties);
        k = FindProperty("_k", properties);
        t = FindProperty("_t", properties);
        fresnelTypeProp = FindProperty("_FresnelType", properties);
        metalTypeProp = FindProperty("_MetalType", properties);
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        //if (oldShader.name != "Davinci/Primitive" && oldShader.name != "Universal Render Pipeline/Lit")
        //    return;

        Texture mainTex = material.GetTexture("_MainTex");
        Color col = Color.white;
        if (material.HasProperty("_Color"))
            col = material.GetColor("_Color");
        else if (material.HasProperty("_MainColor"))
            col = material.GetColor("_MainColor");
        else if (material.HasProperty("_BaseColor"))
            col = material.GetColor("_BaseColor");

        base.AssignNewShaderToMaterial(material, oldShader, newShader);

        material.SetColor("_BaseColor", col);
        material.SetTexture("_MainTex", mainTex);
    }

    public override void OnGUI(MaterialEditor materialEditorIn, MaterialProperty[] properties)
    {
        if (materialEditorIn == null)
            throw new ArgumentNullException("materialEditorIn");

        FindProperties(properties); // MaterialProperties can be animated so we do not cache them but fetch them every event to ensure animated values are updated correctly
        materialEditor = materialEditorIn;
        Material material = materialEditor.target as Material;
        useLinearVectorColor = material.GetFloat("_UseLinearBaseColor") == 1.0f;
        fresnelType = material.GetInt("_FresnelType");
        fresnelTypeProp.intValue = fresnelType;

        // Make sure that needed setup (ie keywords/renderqueue) are set up if we're switching some existing
        // material to a lightweight shader.
        if (m_FirstTimeApply)
        {
            MaterialChanged(material);
            m_FirstTimeApply = false;
        }

        // Use default labelWidth
        EditorGUIUtility.labelWidth = 0f;

        // Detect any changes to the material
        EditorGUI.BeginChangeCheck();
        {
            DoPopup(Styles.materialType, materialTypeProp, Styles.materialNames);
            //DoPopup(Styles.surfaceType, surfaceTypeProp, Styles.surfaceNames);
            //DoPopup(Styles.cullingMode, cullModeProp, Styles.cullNames);

            EditorGUILayout.Space();
            GUILayout.Label(Styles.surfaceProperties, EditorStyles.boldLabel);

            materialEditor.TexturePropertySingleLine(Styles.albedoText, albedoMap, albedoColor);
            Vector4 tiling = material.GetVector("_MainTex_ST");
            useLinearVectorColor = EditorGUILayout.Toggle("Use linear base color", useLinearVectorColor);
            if (useLinearVectorColor)
            {
                materialEditor.ShaderProperty(linearAlbedoColor, Styles.linearAlbeoText);
                useLinearAlbedoColor.floatValue = 1.0f;
            }
            else
            {
                useLinearAlbedoColor.floatValue = 0.0f;
            }

            tiling = EditorGUILayout.Vector4Field(Styles.tilingText, tiling);
            material.SetVector("_MainTex_ST", tiling);
            //albedoTiling.vectorValue = tiling;

            EditorGUILayout.Space();
            materialEditor.TexturePropertySingleLine(Styles.normalText, normalMap);

            EditorGUILayout.Space();
            

            if (materialTypeProp.floatValue == (float)BSDFMaterial.Metal)
            {
                DoPopup(Styles.metalType, metalTypeProp, Styles.metalNames);
                if (metalTypeProp.floatValue != (float)MetalData.MetalType.custom)
                {
                    MetalData.MetalIOR metalIor = MetalData.GetMetalIOR(MetalData.GetMetalName((MetalData.MetalType)metalTypeProp.floatValue));
                    eta.vectorValue = metalIor.eta;
                    k.vectorValue = metalIor.k;
                }
                else
                {
                    
                }
                materialEditor.ShaderProperty(roughnessU, Styles.roughnessUText);
                materialEditor.ShaderProperty(roughnessV, Styles.roughnessVText);
                materialEditor.ShaderProperty(eta, Styles.etaText);
                materialEditor.ShaderProperty(k, Styles.metallicAbsorptionText);

                fresnelTypeProp.floatValue = (float)BSDFFresnel.Conductor;
            }
            else if (materialTypeProp.floatValue == (float)BSDFMaterial.Plastic)
            {
                materialEditor.TexturePropertySingleLine(Styles.glossySpecularMapText, glossySpecularMap, glossySpecularColor);
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(roughnessU, Styles.roughnessUText);
                materialEditor.ShaderProperty(roughnessV, Styles.roughnessVText);
                fresnelTypeProp.floatValue = (float)BSDFFresnel.Dielectric;
            }
            else if (materialTypeProp.floatValue == (float)BSDFMaterial.Mirror)
            {
                fresnelTypeProp.floatValue = (float)BSDFFresnel.NoOp;
            }
            else if (materialTypeProp.floatValue == (float)BSDFMaterial.Glass)
            {
                fresnelTypeProp.floatValue = (float)BSDFFresnel.Dielectric;
                materialEditor.ShaderProperty(t, Styles.transmissionText);
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(roughnessU, Styles.roughnessUText);
                materialEditor.ShaderProperty(roughnessV, Styles.roughnessVText);
                materialEditor.ShaderProperty(eta, Styles.etaText);
                eta.vectorValue = new Vector4(1.5f, 1.5f, 1.5f, 1.0f);
            }
            else if (materialTypeProp.floatValue == (float)BSDFMaterial.Substrate)
            {
                fresnelTypeProp.floatValue = (float)BSDFFresnel.Schlick;
                materialEditor.TexturePropertySingleLine(Styles.glossySpecularMapText, glossySpecularMap, glossySpecularColor);
                //materialEditor.ShaderProperty(glossySpecularColor, Styles.glossySpecularColorText);
                EditorGUILayout.Space();
                materialEditor.ShaderProperty(roughnessU, Styles.roughnessUText);
                materialEditor.ShaderProperty(roughnessV, Styles.roughnessVText);
                materialEditor.ShaderProperty(eta, Styles.etaText);
            }

            EditorGUILayout.Space();


            DoPopup(Styles.fresnelType, fresnelTypeProp, Styles.fresnelNames);
            GUILayout.Label(Styles.function, EditorStyles.boldLabel);
        }

        DoMaterialRenderingOptions();
    }


    protected void DoPopup(string label, MaterialProperty property, string[] options)
    {
        if (property == null)
            throw new ArgumentNullException("property");

        EditorGUI.showMixedValue = property.hasMixedValue;

        var mode = property.floatValue;
        EditorGUI.BeginChangeCheck();
        mode = EditorGUILayout.Popup(label, (int)mode, options);
        if (EditorGUI.EndChangeCheck())
        {
            materialEditor.RegisterPropertyChangeUndo(label);
            property.floatValue = (float)mode;
        }

        EditorGUI.showMixedValue = false;
    }

    protected void DoMaterialRenderingOptions()
    {
        EditorGUILayout.Space();
        //GUILayout.Label(Styles.renderingOptionsLabel, EditorStyles.boldLabel);
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();
    }
}

#endif
