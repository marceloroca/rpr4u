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
        public class RenderSettings
        {
            public RenderMode Mode { get; set; }
            public int ImageWidth { get; set; }
            public int ImageHeight { get; set; }
            public int NumIterations { get; set; }
            public float? RadianceClamp { get; set; }
            public RayDepthSettings RayDepth { get; set; }
        }
    }
}