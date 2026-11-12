using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

namespace XRApartment.EditorTools
{
    internal static class KitchenHdrpMaterials
    {
        // The HDRP 14 editor's Standard converter packs the existing metallic, AO and smoothness channels.
        static readonly Lazy<List<MaterialUpgrader>> Upgraders = new Lazy<List<MaterialUpgrader>>(() =>
        {
            var type = typeof(HDShaderUtils).Assembly.GetType(
                "UnityEditor.Rendering.HighDefinition.UpgradeStandardShaderMaterials", throwOnError: true);
            return (List<MaterialUpgrader>)type.GetMethod("GetHDUpgraders", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
        });

        internal static void UpgradeStandard(Material material)
        {
            MaterialUpgrader.Upgrade(material, Upgraders.Value, MaterialUpgrader.UpgradeFlags.None);
            Validate(material);
        }

        internal static void ConfigureTransparent(Material material, bool preserveSpecular)
        {
            material.SetFloat("_SurfaceType", 1);
            material.SetFloat("_BlendMode", 0);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", preserveSpecular ? 1 : 0);
            Validate(material);
        }

        internal static void Validate(Material material)
        {
            HDShaderUtils.ResetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
        }
    }
}
