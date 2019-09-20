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
using OpenTap;

// This example shows how to publish a single name value pair.
// Using a table analogy, this publishes a single cell in a table.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Results for Single Name Value Pair", Groups: new[] { "Examples", "Plugin Development", "Publish Results" })]
    public class SingleNameValuePair : TestStep
    {   
        public string TableName { get; set;}
        public double RowValue { get; set; }
        public string ColumnName { get; set; }

        public SingleNameValuePair()
        {
            TableName = "SomeTableName";
            ColumnName = "SomeColumnName";
            RowValue = 3.14;
        }

        public override void Run()
        {
            // Behind the scenes, the creates an anonymous class, with a property called ColumnName.
            // The property called ColumnName has a value of RowValue.
            Results.Publish(TableName, new { ColumnName = RowValue });
        }
    }
}
