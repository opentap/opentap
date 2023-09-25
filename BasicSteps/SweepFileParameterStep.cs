using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display("Sweep Parameter From File", "Sweeps the parameters based on a table file format. For example CSV using the CSV plugin..", "Flow Control")]
    public class SweepFileParameterStep : SweepParameterStepBase, ISerializeNotifyAdditionalTypesUsed
    {

        [FilePath]
        [Display("File","File containing the swept values.")]
        public string SweepValues { get; set; }

        /// <summary>
        /// This property declares to the Resource Manager which resources are declared by this test step. 
        /// </summary>
        [AnnotationIgnore]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        public IEnumerable<IResource> Resources
        {
            get
            {
                var table = LoadTable().Item1;
                var parameters = SelectedParameters;
                if (table != null)
                {
                    foreach (var row in table)
                    {
                        for (int i = 0; i < row.Length; i++)
                        {
                            var cell = row[i];
                            var p = parameters.ElementAt(i);
                            if (p != null && p.TypeDescriptor.DescendsTo(typeof(IResource)))
                            {
                                if(StringConvertProvider.TryFromString(cell, p.TypeDescriptor, this, out var r) && r is IResource r2)
                                {
                                    yield return r2;
                                }
                            }
                        }
                    }
                }
            }
        }
            
        public SweepFileParameterStep()
        {
            Name = "Sweep {Parameters}";
            Rules.Add(() => File.Exists(SweepValues), "Sweep values does not exist.", nameof(SweepValues));
        }
        
        public override void PrePlanRun()
        {
            base.PrePlanRun();

            if (SelectedParameters.Count <= 0)
                throw new InvalidOperationException("No parameters selected to sweep");
        }
        
        (string[][], ITableImport) LoadTable()
        {
            var importers = TypeData.GetDerivedTypes<ITableImport>();
            
            foreach (var importType in importers)
            {
                var importer = (ITableImport)importType.CreateInstance();
                if(Path.GetExtension(SweepValues).Equals(importer.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    return (importer.ImportTableValues(SweepValues), importer);
                }
            }
            return (null, null);
        }


        public override void Run()
        {
            base.Run();

            var values = LoadTable().Item1;
            if (values == null)
            {
                throw new Exception("Unable to import table data");
            }
            var originalValues = SelectedParameters.Select(x => x.GetValue(this)).ToArray(); 

            for (int i = 0; i < values.Length; i++)
            {
                string[] row = values[i];
                
                var AdditionalParams = new ResultParameters();

                for (int j = 0; j < row.Length; j++)
                {
                    var p = SelectedParameters[j];

                    string valueString = row[j];

                    object value = row[j];

                    var disp = p.GetDisplayAttribute();
                    AdditionalParams.Add(new ResultParameter(disp.Group.FirstOrDefault() ?? "", disp.Name,
                        valueString));
                    if(StringConvertProvider.TryFromString(valueString, p.TypeDescriptor, this, out var r))
                        p.SetValue(this, value);
                    else
                        throw new Exception($"Unable to convert value '{valueString}' to type {p.TypeDescriptor} for {p.Name}");
                }

                // Notify that values might have changes
                OnPropertyChanged("");

                Log.Info("Running child steps with {0}", string.Join(",", row));

                var runs = RunChildSteps(AdditionalParams, BreakLoopRequested, throwOnBreak: false).ToArray();
                if (BreakLoopRequested.IsCancellationRequested) break;
                runs.ForEach(r => r.WaitForCompletion());
                if (runs.LastOrDefault()?.BreakConditionsSatisfied() == true)
                    break;
            }

            for (int i = 0; i < SelectedParameters.Count; i++)
                SelectedParameters[i].SetValue(this, originalValues[i]);
        }
        
        public ITypeData[] AdditionalTypes => LoadTable().Item2 is ITableImport t ? new[]
        {
            TypeData.GetTypeData(t)
        } : Array.Empty<ITypeData>();
    }
}
