// GPL 3 License
//
// Copyright (c) 2020 Marcelo Roca <mru@marceloroca.com>
//
// This file is part of RPR4U (Radeon ProRender for Unity).
//
// RPR4U is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// RPR4U is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with RPR4U. If not, see<https://www.gnu.org/licenses/>.

using RadeonProRender;
using RadeonProRender.OORPR;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;

namespace RPR4U.RPRUnityEditor
{
    public class SceneData
    {
        private bool isDirty;

        protected SceneData()
        {
            this.isDirty = true;
        }

        public Dictionary<int, CameraData> Cameras { get; set; } = new Dictionary<int, CameraData>();
        public List<LightData> Lights { get; private set; } = new List<LightData>();
        public Dictionary<int, MaterialData> Materials { get; private set; } = new Dictionary<int, MaterialData>();
        public Dictionary<int, MeshInstancesData> Meshes { get; private set; } = new Dictionary<int, MeshInstancesData>();

        public static SceneData GenerateSceneData()
        {
            return new SceneData();
        }

        public void Clear()
        {
            this.Cameras.Clear();
            this.Lights.Clear();
            this.Meshes.Clear();
        }

        public void SetDirty()
        {
            this.isDirty = true;
        }

        public void Update()
        {
            if (!this.isDirty)
            {
                return;
            }

            this.Clear();

            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            this.GetCameras(rootObjects);
            this.GetLights(rootObjects);
            this.GetMeshAndMaterials(rootObjects);

            this.isDirty = false;
        }

        private IEnumerable<T> FindAllObjectsOfType<T>(UnityEngine.GameObject[] rootObjects, bool includeInactive) where T : UnityEngine.Component
        {
            foreach (var go in rootObjects)
            {
                if (go.activeInHierarchy)
                {
                    foreach (T child in go.GetComponentsInChildren<T>(includeInactive))
                    {
                        yield return child;
                    }
                }
            }
        }

        private void GetCameras(UnityEngine.GameObject[] rootObjects)
        {
            foreach (var itemCamera in this.FindAllObjectsOfType<UnityEngine.Camera>(rootObjects, false))
            {
                var cameraId = itemCamera.GetInstanceID();

                if (!this.Cameras.ContainsKey(cameraId))
                {
                    this.Cameras.Add(cameraId, itemCamera.Convert());
                }
            }
        }

        private void GetLights(UnityEngine.GameObject[] rootObjects)
        {
            foreach (var item in this.FindAllObjectsOfType<UnityEngine.Light>(rootObjects, false))
            {
                var rot = item.transform.rotation * UnityEngine.Quaternion.Euler(0, 180, 0);

                var newLight = new LightData
                {
                    Type = item.type.Convert(),
                    Transform = new TransformData(item.transform.position.Convert(), rot.Convert(), item.transform.lossyScale.Convert()),
                    Color = item.color.Convert(),
                    Intensity = item.intensity,
                    Range = item.range,
                    InnerSpotAngle = 0,
                    OuterSpotAngle = item.spotAngle * UnityEngine.Mathf.Deg2Rad,
                    ShadowStrenght = item.shadowStrength,
                };

                this.Lights.Add(newLight);
            }
        }

        private void GetMeshAndMaterials(UnityEngine.GameObject[] rootObjects)
        {
            foreach (var go in rootObjects)
            {
                if (go.activeInHierarchy)
                {
                    var meshRendererList = go.GetComponentsInChildren<UnityEngine.MeshRenderer>(false);

                    foreach (var meshRenderer in meshRendererList)
                    {
                        foreach (var material in meshRenderer.sharedMaterials)
                        {
                            if (material)
                            {
                                var id = material.GetInstanceID();

                                if (!this.Materials.ContainsKey(id))
                                {
                                    switch (material.shader.name.Trim().ToLower())
                                    {
                                        case "standard":
                                            var textureFileName = "";

                                            var transparent = material.GetTag("RenderType", false).Trim().ToLower() == "transparent";

                                            var mainTex = material.GetTexture("_MainTex");
                                            var mainColor = material.color.Convert();

                                            if (mainTex != null)
                                            {
                                                textureFileName = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath.Replace("/Assets", ""), UnityEditor.AssetDatabase.GetAssetPath(mainTex.GetInstanceID())));
                                            }

                                            if (transparent)
                                            {
                                                if (mainTex != null)
                                                {
                                                    this.Materials.Add(id, new MaterialData { MainTexture = textureFileName, MainColor = mainColor, Transparent = true }); ;
                                                }
                                                else
                                                {
                                                    this.Materials.Add(id, new MaterialData { MainColor = mainColor, Transparent = true });
                                                }
                                            }
                                            else
                                            {
                                                if (mainTex != null)
                                                {
                                                    this.Materials.Add(id, new MaterialData { MainTexture = textureFileName });
                                                }
                                                else
                                                {
                                                    this.Materials.Add(id, new MaterialData { MainColor = mainColor });
                                                }
                                            }

                                            break;

                                        default:
                                            UnityEngine.Debug.LogError("Shader Not implemented");
                                            UnityEngine.Debug.Log($"material.name={material.name} material.shader.name={material.shader.name} renderType = {material.GetTag("RenderType", false)}");
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                UnityEngine.Debug.Log($"{meshRenderer.gameObject.name} no tiene material", meshRenderer.gameObject);
                            }
                        }

                        var meshFilter = meshRenderer.GetComponent<UnityEngine.MeshFilter>();

                        if (meshFilter != null)
                        {
                            if (meshFilter.sharedMesh == null)
                            {
                                UnityEngine.Debug.Log($"{meshFilter.gameObject.name} mesh filter do not have mesh asigned", meshFilter.gameObject);
                            }
                            else
                            {
                                var id = meshFilter.sharedMesh.GetInstanceID();

                                if (this.Meshes.TryGetValue(id, out MeshInstancesData meshData))
                                {
                                    meshData.Add(meshRenderer, meshFilter);
                                }
                                else
                                {
                                    meshData = new MeshInstancesData(meshRenderer, meshFilter);

                                    this.Meshes.Add(id, meshData);
                                }
                            }
                        }
                    }
                }
            }
        }

        public class InstanceData
        {
            public TransformData Transform { get; set; }
            public int[] MaterialsId { get; set; }
        }

        public class MeshInstancesData
        {
            public MeshInstancesData()
            {
                this.SubMeshes = new List<Mesh>();
                this.Instances = new List<InstanceData>();
            }

            public MeshInstancesData(UnityEngine.MeshRenderer meshRenderer, UnityEngine.MeshFilter meshFilter)
                : this()
            {
                for (var i = 0; i < UnityEngine.Mathf.Min(meshFilter.sharedMesh.subMeshCount, meshRenderer.sharedMaterials.Count()); ++i)
                {
                    this.SubMeshes.Add(meshFilter.sharedMesh.GetSubmesh(i).Convert());
                }

                this.Add(meshRenderer, meshFilter);
            }

            public List<Mesh> SubMeshes { get; set; }
            public List<InstanceData> Instances { get; set; }

            public void Add(UnityEngine.MeshRenderer meshRenderer, UnityEngine.MeshFilter meshFilter)
            {
                var materialsId = new int[meshFilter.sharedMesh.subMeshCount];

                for (var i = 0; i < UnityEngine.Mathf.Min(meshFilter.sharedMesh.subMeshCount, meshRenderer.sharedMaterials.Count()); ++i)
                {
                    materialsId[i] = meshRenderer.sharedMaterials[i].GetInstanceID();
                }

                this.Instances.Add(new InstanceData { Transform = meshRenderer.transform.Convert(), MaterialsId = materialsId });
            }
        }
    }
}