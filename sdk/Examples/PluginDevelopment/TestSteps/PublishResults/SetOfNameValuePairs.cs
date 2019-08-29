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

// This example shows how to publish a set of name/value pairs.
// Using a table analogy, this publishes a single row.
// If you have multiple rows to publish, you should use the PublishTable method.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Results for Name Value Pairs", Groups: new[] { "Examples", "Plugin Development", "Publish Results" }, 
        Description: "A set of name value pairs, i.e. a row of N values.")]
    
    public class SetOfNameValuePairs : TestStep
    {
        public int MyFirstInt { get; set; }
        public int MySecondInt { get; set; }
        public string MyString { get; set; }
        public bool MyBool { get; set; }
        public double MyDouble { get; set; }

        public SetOfNameValuePairs()
        {
            MyFirstInt = 1;
            MySecondInt = 2;
            MyString = "SomeString";
            MyBool = true;
            MyDouble = 3.456;
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();  
        }

        public override void Run()
        {   
            List<string> names = new List<string>
            {
                "MyFirstIntColumn",
                "MySecondIntColumn",
                "MyStringColumn",
                "MyBoolColumn",
                "MyDoubleColumn"
            };

            List<IConvertible> values =  new List<IConvertible> {MyFirstInt, MySecondInt, MyString, MyBool, MyDouble};
            // In the line below,  the IConvertible list is converted to an array.
            // This makes it compatible with the Publish call.
            Results.Publish("MyTableName", names, values.ToArray());
        }

        public override void PostPlanRun()
        {
            base.PostPlanRun(); 
        }
    }
}
