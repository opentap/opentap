//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using System;
using System.Collections.Generic;
using OpenTap;

// The PublishTable method is the fastest way to store results.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Results for XY Array", Groups: new[] { "Examples", "Plugin Development", "Publish Results" }, 
        Description: "This example shows how to store results for XY array with optional limits.")]
    public class ResultsForXyArray : TestStep
    {
        [Display("Point Count", Order: 1, Description: "The number of points to generate.")]
        public int PointCount { get; set; }

        [Display("Enable Limits", Order: 2, Description: "Enables limits to be stored with data.")]
        public bool LimitsEnabled { get; set; }

        public ResultsForXyArray()
        {
            PointCount = 10;
            LimitsEnabled = true;
        }

        public override void Run()
        {
            // Generate data to be stored.
            int[] xValues = new int[PointCount];
            double[] yValues = new double[PointCount];
            for (var i = 0; i < PointCount; i++)
            {
                xValues[i] = 10 * i;
                yValues[i] = Math.Sin(i * 2 * Math.PI / PointCount);
            }

            // Generate limits to be stored.
            double[] yLimitHigh = new double[PointCount];
            double[] yLimitLow = new double[PointCount];
            if (LimitsEnabled)
            {
                for (var i = 0; i < PointCount; i++)
                {
                    yLimitHigh[i] = yValues[i] + 0.1;
                    yLimitLow[i] = yValues[i] - 0.2;
                }
            }

            // Store results.
            if (!LimitsEnabled)
            {
                // Use PublishTable when possible, as it has the highest performance.
                Results.PublishTable("X versus Y", new List<string> { "X Values", "Y Values" }, xValues, yValues);
            }
            else
            {
                Results.PublishTable("X versus Y", new List<string> { "X Values", "Y Values", "High Limit", "Low Limit" }, xValues, yValues, yLimitHigh, yLimitLow);
            }
        }
    }
}
