/*
 * Nova Menu  Patches/Menu/ShaderFix.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Poison Software
 * Copyright (C) 2026  HZMGTX
 * https://github.com/HZMGTX/Nova
 *
 * Modified from Poison Menu (formerly Seralyth Menu)
 * https://github.com/heycanihavethis/Poison
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using static Nova.Menu.Main;

namespace Nova.Patches.Menu
{
    [HarmonyPatch(typeof(GameObject), nameof(GameObject.CreatePrimitive))]
    public class ShaderFix
    {
        private static Shader litShader;
        private static Shader unlitShader;
        private static Shader uberShader;

        private static Shader FindShader(ref Shader cache, string name)
        {
            if (cache == null)
                cache = Shader.Find(name);

            return cache;
        }

        private static void Postfix(GameObject __result)
        {
            Renderer renderer = __result.GetComponent<Renderer>();
            if (renderer == null)
                return;

            bool crystal = crystallizeMenu && CrystalMaterial != null;
            if (crystal)
                renderer.material = CrystalMaterial;

            // Accessing .material creates a per-object instance, so only do it once
            Material material = renderer.material;

            if (!crystal)
            {
                if (transparentMenu)
                {
                    material.shader = shinyMenu ? FindShader(ref litShader, "Universal Render Pipeline/Lit") : FindShader(ref unlitShader, "Universal Render Pipeline/Unlit");

                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_Blend", 0);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                    material.shader = shinyMenu ? FindShader(ref litShader, "Universal Render Pipeline/Lit") : FindShader(ref uberShader, "GorillaTag/UberShader");
            }

            material.color = backgroundColor.GetColor(0);

            if (material != CrystalMaterial)
                __result.AddComponent<MaterialInstanceCleanup>().material = material;
        }
    }

    // Destroys the material instance created by ShaderFix along with its object
    public class MaterialInstanceCleanup : MonoBehaviour
    {
        public Material material;

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
