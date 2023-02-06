using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display("Sweep Parameters From File", "Table based loop that sweeps the value of its parameters based on a file.", "Flow Control")]
    public class SweepFromFileStep : SweepParameterStep
    {
        /// TODO:
        /// * Make parameters non-editable
        /// * Figure out a good way to preview the first row of parameters and values
        /// * Create method for mapping names from the file to the names of paramterized members
        /// ** Possibly by TypeData magic to add a member for each parameter, with AvailableValues set to the file's column names
        /// * Check if editors support multiple extensions (either comma-separated or by adding multiple FilePathAttributes)
        /// ** Use typedata magic to set file extensions based on installed plugins 
        [FilePath(FilePathAttribute.BehaviorChoice.Open, "")]
        [Display("File Path")]
        public string FilePath { get; set; }

        [Browsable(true)]
        public void LoadFile()
        {
            var ext = Path.GetExtension(FilePath);
            var importers = TypeData.GetDerivedTypes<ITableImport>()
                .Where(i => i.CanCreateInstance)
                .TrySelect(i => i.CreateInstance() as ITableImport, _ => { })
                .Where(i => i != null)
                .Where(i => i.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));

            var imported = false;
            foreach (var importer in importers)
            {
                try
                {
                    var matrix = importer.ImportTableValues(FilePath);
                    var annotations = AnnotationCollection.Annotate(SweepValues);
                    var table = new TableView(annotations);
                    if (table.SetMatrix(matrix, null, true))
                    {
                        table = new TableView(annotations);
                        table.SetMatrix(matrix, null, false);
                    }
                    annotations.Write();
                    imported = true;
                }
                catch
                {
                    continue;
                }
            }

            if (!imported)
            {
                Log.Error($"No plugin could handle the file '{FilePath}'.");
            }

        }
    }
}

