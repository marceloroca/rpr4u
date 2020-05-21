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

namespace RPR4U.RPRUnityEditor.Data
{
    public partial class SceneSettings
    {
        public static SceneSettings Default
        {
            get
            {
                return new SceneSettings
                {
                    Render = new RenderSettings
                    {
                        Mode = RenderMode.GlobalIllumination,
                        ImageWidth = 1024,
                        ImageHeight = 512,
                        NumIterations = 100,
                    },
                    Viewport = new ViewportSettings
                    {
                        ImageWidth = 1024,
                        ImageHeight = 512,
                        NumIterations = 100,
                    },
                    Camera = new CameraSettings
                    {
                        Mode = CameraMode.Perspective,
                        IPD = .65f,
                        SelectedCamera = 0,
                    },
                    Adaptative = new AdaptativeSettings
                    {
                        Enabled = false,
                        MinSamples = 100,
                        Threshold = .05f,
                        TileSize = 16,
                    },
                    Light = new LightSettings
                    {
                        DirectionalLightMultiplier = 6,
                        PointLightMultiplier = 2,
                        SpotLightMultiplier = 2,
                    },
                };
            }
        }

        public class CameraSettings
        {
            public CameraMode Mode { get; set; }
            public float IPD { get; set; }
            public int SelectedCamera { get; set; }
        }
    }
}