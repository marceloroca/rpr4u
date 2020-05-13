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
        public Dictionary<int, MeshTransformData> MeshTransforms { get; private set; } = new Dictionary<int, MeshTransformData>();

        public static SceneData GenerateSceneData()
        {
            return new SceneData();
        }

        public void Clear()
        {
            this.Cameras.Clear();
            this.Lights.Clear();
            this.MeshTransforms.Clear();
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
                // var rot = item.transform.rotation * UnityEngine.Quaternion.Euler(0, 180, 180);
                var rot = item.transform.rotation * UnityEngine.Quaternion.Euler(0, 180, 0);

                var newLight = new LightData
                {
                    Type = item.type.Convert(),
                    Transform = new TransformData(item.transform.position.Convert(), rot.Convert(), item.transform.lossyScale.Convert()),
                    // Transform = item.transform.Convert(),
                    Color = item.color.Convert(),
                    Intensity = item.intensity,
                    Range = item.range,
                    InnerSpotAngle = 0,
                    OuterSpotAngle = item.spotAngle * UnityEngine.Mathf.Deg2Rad
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
                                    var mainTex = material.GetTexture("_MainTex");

                                    if (mainTex != null)
                                    {
                                        var textureFileName = Path.Combine(UnityEngine.Application.dataPath.Replace("/Assets", ""), UnityEditor.AssetDatabase.GetAssetPath(mainTex.GetInstanceID()));
                                        this.Materials.Add(id, new MaterialData { MainTexture = textureFileName });
                                    }
                                    else
                                    {
                                        this.Materials.Add(id, new MaterialData { MainColor = material.color.Convert() });
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

                                if (this.MeshTransforms.TryGetValue(id, out MeshTransformData meshData))
                                {
                                    meshData.Add(meshRenderer);
                                }
                                else
                                {
                                    meshData = new MeshTransformData(meshRenderer, meshFilter);

                                    this.MeshTransforms.Add(id, meshData);
                                }
                            }
                        }
                    }
                }
            }
        }

        public class MeshData
        {
            public int MaterialId { get; set; }
            public Mesh Mesh { get; set; }
        }

        public class MeshTransformData
        {
            public MeshTransformData()
            {
                this.SubMeshes = new List<MeshData>();
                this.Transforms = new List<TransformData>();
            }

            public MeshTransformData(UnityEngine.MeshRenderer meshRenderer, UnityEngine.MeshFilter meshFilter)
                : this()
            {
                if (meshFilter.sharedMesh.subMeshCount > 1)
                {
                    for (var i = 0; i < UnityEngine.Mathf.Min(meshFilter.sharedMesh.subMeshCount, meshRenderer.sharedMaterials.Count()); ++i)
                    {
                        this.SubMeshes.Add(new MeshData { MaterialId = meshRenderer.sharedMaterials[i].GetInstanceID(), Mesh = meshFilter.sharedMesh.GetSubmesh(i).Convert() });
                    }
                }
                else
                {
                    this.SubMeshes.Add(new MeshData { MaterialId = meshRenderer.sharedMaterial.GetInstanceID(), Mesh = meshFilter.sharedMesh.Convert() });
                }

                this.Transforms.Add(meshRenderer.transform.Convert());
            }

            public List<MeshData> SubMeshes { get; set; }
            public List<TransformData> Transforms { get; set; }

            public void Add(UnityEngine.MeshRenderer meshRenderer)
            {
                this.Transforms.Add(meshRenderer.transform.Convert());
            }
        }
    }
}