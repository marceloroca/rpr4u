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
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO.Compression;
using System.IO;
using RPR4U.RPRUnityEditor.Data;

namespace RPR4U.RPRUnityEditor
{
    public class SceneRender : IDisposable
    {
        private RPRContext context;
        private RPRScene scene;
        private RPRFrameBuffer frameBufferRender;
        private RPRFrameBuffer frameBufferTexture;

        //  private SizeInt imageSize;
        private bool isAccesingBuffer;

        // private int numIterations;
        private readonly string pluginFolder;

        private bool stopRender;
        private bool isRenderingFrame;
        private SceneSettings sceneSettings;

        public SceneRender(string pluginFolder)
        {
            this.pluginFolder = pluginFolder;
        }

        public bool IsRendering { get; private set; }
        public int IterationCount { get; private set; }
        public bool IsInitialized { get; private set; }

        public TimeSpan RenderingTime { get; private set; }

        public void Dispose()
        {
            this.context?.Dispose();
        }

        public UnityEngine.Texture2D GetTexture()
        {
            if (this.context == null)
            {
                return null;
            }

            this.isAccesingBuffer = true;

            while (this.isRenderingFrame)
            {
                Thread.Sleep(10);
            }

            this.context.ResolveFrameBuffer(this.frameBufferRender, this.frameBufferTexture, false);

            var tex = new UnityEngine.Texture2D(this.sceneSettings.RenderSettings.ImageWidth, this.sceneSettings.RenderSettings.ImageHeight, UnityEngine.TextureFormat.RGBAFloat, false, true);

            this.isAccesingBuffer = false;

            tex.SetPixelData(this.frameBufferTexture.GetImage(), 0, 0);
            tex.filterMode = UnityEngine.FilterMode.Point;
            tex.Apply(updateMipmaps: false);

            return tex;
        }

        public void Initialize(CreationFlags creationFlags, SceneSettings sceneSettings)
        {
            if (this.context != null)
            {
                this.context.Dispose();
                this.frameBufferRender = null;
                this.frameBufferTexture = null;
                this.scene = null;
            }

            this.sceneSettings = sceneSettings;

            var in_props = new IntPtr[16];
            in_props[0] = (IntPtr)0;

            if (sceneSettings.AdaptativeSettings.Enabled)
            {
                in_props[0] = (IntPtr)ContextInfo.SamplerType;
                in_props[1] = (IntPtr)ContextSamplerType.Cmj;
                in_props[2] = (IntPtr)0;
            }

            this.context = new RPRContext(creationFlags, this.pluginFolder, in_props);

            this.context.SetRenderMode(sceneSettings.RenderSettings.Mode);
            this.context.SetParameter("preview", 1u);

            if (sceneSettings.AdaptativeSettings.Enabled)
            {
                this.context.SetParameter("as.tilesize", sceneSettings.AdaptativeSettings.TileSize);
                this.context.SetParameter("as.minspp", sceneSettings.AdaptativeSettings.MinSamples);
                this.context.SetParameter("as.threshold", sceneSettings.AdaptativeSettings.Threshold);
            }

            this.scene = new RPRScene(this.context);
            this.scene.SetCurrent();

            var sceneData = SceneData.GenerateSceneData();
            sceneData.Update();

            this.frameBufferRender = new RPRFrameBuffer(context, new FramebufferFormat(4, ComponentType.Float32), new FramebufferDesc(sceneSettings.RenderSettings.ImageWidth, sceneSettings.RenderSettings.ImageHeight));
            context.SetAOV(this.frameBufferRender, AOV.Color);

            this.frameBufferTexture = new RPRFrameBuffer(context, new FramebufferFormat(4, ComponentType.Float32), new FramebufferDesc(sceneSettings.RenderSettings.ImageWidth, sceneSettings.RenderSettings.ImageHeight));

            var postEffect = new RPRPostEffect(context, PostEffectType.Normalization);
            context.Attach(postEffect);

            var materialTable = new Dictionary<int, RPRMaterial>();

            foreach (var itemMaterial in sceneData.Materials)
            {
                if (!materialTable.ContainsKey(itemMaterial.Key))
                {
                    UberMaterial uberMaterial;

                    if (itemMaterial.Value.Transparent)
                    {
                        uberMaterial = new UberMaterial
                        {
                            Reflection = new UberMaterial.ReflectionData
                            {
                                Weight = new UberMaterial.Vector4Node(new Vector4(1)),
                                Color = new UberMaterial.ColorNode(new Color(0.501961f, 0.501961f, 0.501961f, 0)),
                                //Roughness = new UberMaterial.ImageTextureNode(itemMaterial.Value.MainTexture),
                                Mode = UberMaterialIorMode.Pbr,
                                IOR = new UberMaterial.Vector4Node(new Vector4(1.52f)),
                                Anisotropy = new UberMaterial.Vector4Node(new Vector4(0)),
                                AnisotropyRotation = new UberMaterial.Vector4Node(new Vector4(0)),
                            },
                            Refraction = new UberMaterial.RefractionData
                            {
                                Weight = new UberMaterial.Vector4Node(new Vector4(1)),
                                Color = new UberMaterial.ColorNode(new Color(0.960784f, 1, 0.988235f, 0)),
                                IOR = new UberMaterial.Vector4Node(new Vector4(1.05f)),
                                AbsortionDistance = new UberMaterial.Vector4Node(new Vector4(0)),
                                AbsortionColor = new UberMaterial.ColorNode(new Color(1, 1, 1, 0)),
                            }
                        };
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(itemMaterial.Value.MainTexture))
                        {
                            uberMaterial = UberMaterial.DiffuseSimple(itemMaterial.Value.MainColor);
                        }
                        else
                        {
                            uberMaterial = UberMaterial.DiffuseSimple(itemMaterial.Value.MainTexture);
                        }
                    }

                    var rprMaterial = new RPRUberMaterial(this.context, uberMaterial);

                    materialTable.Add(itemMaterial.Key, rprMaterial);
                }
            }

            foreach (var itemMesh in sceneData.Meshes)
            {
                var csm = 0;

                foreach (var itemSubMesh in itemMesh.Value.SubMeshes)
                {
                    // creamos el mesh
                    var rprMesh = new RPRMesh(this.context, itemSubMesh);

                    // instanciamos
                    var cin = 0; ;
                    foreach (var itemInstance in itemMesh.Value.Instances)
                    {
                        if (cin < 1)
                        {
                            rprMesh.SetTRS(itemInstance.Transform);
                            scene.Attach(rprMesh);
                            // le metemos el material
                            rprMesh.SetMaterial(materialTable[itemInstance.MaterialsId[csm]]);
                        }
                        else
                        {
                            var rprInstance = new RPRMeshInstance(this.context, rprMesh);
                            rprInstance.SetTRS(itemInstance.Transform);
                            scene.Attach(rprInstance);
                            // le metemos el material
                            rprInstance.SetMaterial(materialTable[itemInstance.MaterialsId[csm]]);
                        }

                        ++cin;
                    }

                    ++csm;
                }
            }

            // creamos lights
            foreach (var itemLigh in sceneData.Lights)
            {
                RPRLight rprLight = null;

                switch (itemLigh.Type)
                {
                    case LightType.Spot:
                        var rprSpotLight = new RPRSpotLight(this.context);
                        rprSpotLight.SetRadiantPower(itemLigh.Color * itemLigh.Intensity * sceneSettings.LightSettings.SpotLightMultiplier);
                        rprSpotLight.SetConeShape(itemLigh.InnerSpotAngle, itemLigh.OuterSpotAngle);

                        rprLight = rprSpotLight;
                        break;

                    case LightType.Point:
                        var rprPointLight = new RPRPointLight(this.context);
                        rprPointLight.SetRadiantPower(itemLigh.Color * itemLigh.Intensity * sceneSettings.LightSettings.PointLightMultiplier);

                        rprLight = rprPointLight;
                        break;

                    case LightType.Directional:
                        var rprDirectionalLight = new RPRDirectionalLight(this.context);
                        rprDirectionalLight.SetRadiantPower(itemLigh.Color * itemLigh.Intensity * sceneSettings.LightSettings.DirectionalLightMultiplier);
                        rprDirectionalLight.SetShadowSoftness(itemLigh.ShadowStrenght);

                        rprLight = rprDirectionalLight;
                        break;

                    default:
                        UnityEngine.Debug.LogError($"Light {itemLigh.Type} not implemented");
                        break;
                }

                if (rprLight != null)
                {
                    rprLight.SetTRS(itemLigh.Transform);
                    scene.Attach(rprLight);
                }
            }

            var cameras = new Dictionary<int, RPRCamera>();

            var i = 0;
            RPRCamera selectedCamera = null;

            foreach (var itemCamera in sceneData.Cameras)
            {
                var camera = new RPRCamera(this.context);

                camera.SetTRS(itemCamera.Value.Transform);

                camera.SetFocalLength(itemCamera.Value.FocalLenght);

                var sensorSize = itemCamera.Value.SensorSize;

                var aspectRatio = this.sceneSettings.RenderSettings.ImageHeight / (float)this.sceneSettings.RenderSettings.ImageWidth;

                camera.SetSensorSize(new SizeFloat(sensorSize.Width, sensorSize.Width * aspectRatio));

                camera.SetLensShift(itemCamera.Value.LensShift);
                camera.SetClippingPlanes(itemCamera.Value.ClippingPlanes.X, itemCamera.Value.ClippingPlanes.Y);

                cameras.Add(itemCamera.Key, camera);

                if (i == 0 || i == sceneSettings.CameraSettings.SelectedCamera)
                {
                    selectedCamera = camera;
                }

                ++i;
            }

            if (selectedCamera != null)
            {
                this.scene.SetCurrentCamera(selectedCamera);
                selectedCamera.SetMode(sceneSettings.CameraSettings.Mode);

                if (sceneSettings.CameraSettings.Mode == CameraMode.LatitudLongitudeStereo || sceneSettings.CameraSettings.Mode == CameraMode.CubemapStereo)
                {
                    selectedCamera.SetIPD(sceneSettings.CameraSettings.IPD);
                }

                this.IsInitialized = true;
            }
        }

        public void SaveRender(string path)
        {
            this.frameBufferRender?.SaveToFile(path);
        }

        public void ExportScene(string path, bool compress)
        {
            this.scene?.Export(path);

            var json = UnityEngine.JsonUtility.ToJson(this.sceneSettings, true);

            var settingsPath = path + ".jscene";

            File.WriteAllText(settingsPath, json);

            if (compress)
            {
                File.Delete(path + ".z");

                using var zip = ZipFile.Open(path + ".z", ZipArchiveMode.Create);

                zip.CreateEntryFromFile(path, (new FileInfo(path)).Name, CompressionLevel.Optimal);
                zip.CreateEntryFromFile(settingsPath, (new FileInfo(settingsPath)).Name, CompressionLevel.Optimal);

                File.Delete(path);
                File.Delete(settingsPath);
            }
        }

        public void StartRender()
        {
            this.IsRendering = true;

            this.stopRender = false;
            this.IterationCount = 0;
            var startTime = DateTime.Now;

            var renderThread = new Thread(() =>
            {
                while (!this.stopRender)
                {
                    while (this.isAccesingBuffer)
                    {
                        Thread.Sleep(10);
                    }

                    this.isRenderingFrame = true;

                    if (this.sceneSettings.AdaptativeSettings.Enabled)
                    {
                        this.context.SetParameter("framecount", this.IterationCount);
                    }

                    this.context.Render();
                    ++this.IterationCount;
                    this.isRenderingFrame = false;

                    this.RenderingTime = DateTime.Now - startTime;

                    if (this.IterationCount > this.sceneSettings.RenderSettings.NumIterations)
                    {
                        this.stopRender = true;
                    }

                    Thread.Sleep(10);
                }

                this.IsRendering = false;
            });

            renderThread.Start();
        }

        public void StopRender()
        {
            this.stopRender = true;

            while (this.isRenderingFrame)
            {
                Thread.Sleep(10);
            }
        }
    }
}