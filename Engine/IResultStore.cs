//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;

namespace OpenTap
{
    #region Data structure
    /// <summary>
    /// A named object.
    /// </summary>
    public interface IAttributedObject
    {
        /// <summary>
        /// Name of this object.  
        /// </summary>
        string Name { get; }
        /// <summary>
        /// String describing this object.  
        /// </summary>
        string ObjectType { get; }
    }

    /// <summary>
    /// A named parameter.
    /// </summary>
    public interface IParameter : IAttributedObject
    {
        /// <summary>
        /// Optional name of the group of parameters to which this parameter belongs.  
        /// </summary>
        string Group { get; }

        /// <summary>
        /// Value of this parameter.  
        /// </summary>
        IConvertible Value { get; }
    }

    /// <summary>
    /// A list of parameters, with a string indexer.
    /// </summary>
    public interface IParameters : IList<IParameter>
    {
        /// <summary>
        /// Get a parameter by name.
        /// </summary>
        /// <returns>Null if the parameter was not found</returns>
        IConvertible this[string Name] { get; }
    }

    /// <summary>
    /// An object in a hierarchy with a name and some named properties.
    /// </summary>
    public interface IData : IAttributedObject
    {
        /// <summary>
        /// Parent of this object. 
        /// </summary>
        IData Parent { get; }
        /// <summary>
        /// All parameters that describes this object.
        /// </summary>
        IParameters Parameters { get; }

        /// <summary>
        /// Returns an ID that identifies this object.
        /// </summary>
        /// <returns></returns>
        long GetID();
    }

    /// <summary>
    /// Contains data that has the same table name, column names, and result data types as a <see cref="ResultTable"/>.
    /// </summary>
    public interface IResultTable : IData
    {
        /// <summary>
        /// Array containing the result columns.  
        /// </summary>
        IResultColumn[] Columns { get; }
    }

    /// <summary>
    /// Interface to store <see cref="IResultTable"/> column data.
    /// </summary>
    public interface IResultColumn : IAttributedObject
    {
        /// <summary>
        /// Data in the column.  
        /// </summary>
        Array Data { get; }
    }
    
    /// <summary>
    /// An "extensible Enum" that can be used to describe attachments.
    /// </summary>
    public class AttachmentType : IEquatable<AttachmentType>
    {
        #region Support
        private string _TypeName;

        /// <summary>
        /// Creates an attachment type object.
        /// </summary>
        public AttachmentType(string TypeName)
        {
            this._TypeName = TypeName;
        }

        /// <summary>
        /// Compares this object to another.
        /// </summary>
        bool IEquatable<AttachmentType>.Equals(AttachmentType other)
        {
            return _TypeName == other._TypeName;
        }

        /// <summary>
        /// Compares this object to another.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is AttachmentType)
                return _TypeName == ((AttachmentType)obj)._TypeName;
            else
                return false;
        }

        /// <summary>
        /// Returns the hashcode for this object.
        /// </summary>
        public override int GetHashCode()
        {
            return _TypeName.GetHashCode();
        }
        #endregion

        /// <summary>
        /// A log file.
        /// </summary>
        public static AttachmentType LogFile { get { return new AttachmentType("LogFile"); } }
        /// <summary>
        /// A TestPlan XML file.
        /// </summary>
        public static AttachmentType TestPlan { get { return new AttachmentType("TestPlan"); } }
    }
#endregion

#region Searching
    /// <summary>
    /// The operation for <see cref="SearchComparison"/>.
    /// </summary>
    public enum ComparisonOp
    {
        /// <summary>Two specified values must be the same.</summary>
        Equal,
        /// <summary>Two specified values must be different.</summary>
        NotEqual,
        /// <summary>Parameter must be less than.</summary>
        Less,
        /// <summary>Parameter must be less than or equal.</summary>
        LessEqual,
        /// <summary>Parameter must be greater than.</summary>
        Greater,
        /// <summary>Parameter must be greater than or equal.</summary>
        GreaterEqual,

        /// <summary>Parameter must be similar to the value.</summary>
        /// <remarks>This is guaranteed to be true if the two values are equal, but is not otherwise guaranteed to work in any specific way.</remarks>
        Like,

        /// <summary>
        /// Value must exist as a parameter name or value. 
        /// </summary>
        Exists
    }

    /// <summary>
    /// Operation for <see cref="SearchBinaryOp"/>.
    /// </summary>
    public enum BinOp
    {
        /// <summary>
        /// Specifies that both conditions must be satisfied.
        /// </summary>
        And,
        /// <summary>
        /// Specifies that at least one of the two conditions must be satisfied.
        /// </summary>
        Or
    }

    /// <summary>
    /// The basic search operand from which the other search operands are derived. 
    /// </summary>
    public abstract class SearchOp
    {
    }

    /// <summary>
    /// Matches all children of the specified parents.
    /// </summary>
    public class SearchChildOfOp : SearchOp
    {
        /// <summary>
        /// The list of elements to match the children of.
        /// </summary>
        public List<IData> Parents { get; set; }
    }

    /// <summary>
    /// Matches the last test plan run.
    /// </summary>
    public class SearchLastRun : SearchOp
    {
        /// <summary>
        /// Number of the last runs to select.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Matches the last test plan run.
        /// </summary>
        public SearchLastRun()
        {
            Count = 1;
        }
    }

    /// <summary>
    /// A binary operation between two other operations.
    /// </summary>
    public class SearchBinaryOp : SearchOp
    {
        /// <summary>
        /// Left-side operand.  
        /// </summary>
        public SearchOp A { get; set; }

        /// <summary>
        /// Operation to perform.  
        /// </summary>
        public BinOp Op { get; set; }

        /// <summary>
        /// Right-side operand.  
        /// </summary>
        public SearchOp B { get; set; }
    }

    /// <summary>
    /// Comparison between a named parameter and a value.
    /// </summary>
    public class SearchComparison : SearchOp
    {
        /// <summary>
        /// Scope of the parameter to match ("", "plan", or "step").  
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// GroupName of the parameter to match (leave empty to match any group).  
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Name of the parameter to match.  
        /// </summary>
        public string Parameter { get; set; }

        /// <summary>
        /// Operation to perform.  
        /// </summary>
        public ComparisonOp Op { get; set; }

        /// <summary>
        /// Value to compare against the right-side value.  
        /// </summary>
        public IConvertible Value { get; set; }
    }

    /// <summary>
    /// Comparison between a named parameter and a value.
    /// </summary>
    public class SearchRange : SearchOp
    {
        /// <summary>
        /// The scope of the parameter to match. Could be "plan" or "step".
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// The name of the parameter to match.
        /// </summary>
        public string Parameter { get; set; }

        /// <summary>
        /// The value to compare against as the right-hand side.
        /// </summary>
        public ICombinedNumberSequence<long> Value { get; set; }
    }

    /// <summary>
    /// The conditions for what to search for.
    /// </summary>
    public class SearchCondition
    {
        /// <summary>
        /// When true, specifies that any matched tree nodes automatically match all parents of that <see cref="IData"/> element.  
        /// </summary>
        public bool GetParents { get; set; }

        /// <summary>
        /// When true, specifies that a matched tree root node automatically matches all children of that given <see cref="IData"/> element.  
        /// </summary>
        public bool GetChildren { get; set; }

        /// <summary>
        /// The tree of <see cref="SearchOp"/> conditions to match for.  
        /// </summary>
        public SearchOp Comparison { get; set; }

        /// <summary>
        /// Condition that will match all elements in the result store.  
        /// </summary>
        /// <returns></returns>
        public static SearchCondition All()
        {
            return new SearchCondition { GetChildren = true, GetParents = true, Comparison = null };
        }

        /// <summary>
        /// Condition that will match any elements in a list as well as all children of those elements.  
        /// </summary>
        /// <param name="testPlanRunIds"></param>
        public static SearchCondition ChildrenOf(IEnumerable<IData> testPlanRunIds)
        {
            return new SearchCondition { Comparison = new SearchChildOfOp { Parents = testPlanRunIds.ToList() }, GetChildren = true, GetParents = false };
        }

        /// <summary>
        /// Condition that will match all elements in the result store.  
        /// </summary>
        /// <returns></returns>
        public static SearchCondition LastRun()
        {
            return new SearchCondition { GetChildren = true, GetParents = true, Comparison = new SearchLastRun() };
        }
    }
    #endregion

    /// <summary>
    /// A structure containing limit set data.
    /// </summary>
    public class LimitSet
    {
        /// <summary>
        /// The friendly name of the limit set.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The limits in this limit set.
        /// </summary>
        public List<Limit> Limits { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LimitSet"/> class.
        /// </summary>
        /// <param name="name"></param>
        public LimitSet(string name)
        {
            Name = name;
            Limits = new List<Limit>();
        }
    }

    /// <summary>
    /// A single limit from a limit set.
    /// </summary>
    public class Limit : ValidatingObject
    {
        /// <summary>
        /// The result name to match.
        /// </summary>
        [Display("Result Name", Description: "The name of the result to match.", Order: -10)]
        public string ResultName { get; set; }
        /// <summary>
        /// The result column to which the limits are applied.
        /// </summary>
        [Display("Column Name", Description: "The name of the result column to apply limit to.", Order: -9)]
        public string ColumnName { get; set; }
        /// <summary>
        /// The lower limit to apply to the result.
        /// </summary>
        [Display("Lower Limit", Description: "The lower limit. To pass the column value must be above this.", Order: -8)]
        public double LowerLimit { get; set; }
        /// <summary>
        /// The upper limit to apply to the result.
        /// </summary>
        [Display("Upper Limit", Description: "The upper limit. To pass the column value must be below this.", Order: -7)]
        public double UpperLimit { get; set; }
        /// <summary>
        /// The conditions that have to apply for this limit.
        /// </summary>
        [Display("Conditions", Description: "Additional conditions that apply for this limit. All conditions must evaluate to true for this limit to be used.", Order: -6)]
        public List<LimitCondition> Conditions { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Limit"/> class.
        /// </summary>
        public Limit()
        {
            Rules.Add(() => !string.IsNullOrWhiteSpace(ResultName), "Result Name must be valid.", "ResultName");
            Rules.Add(() => !string.IsNullOrWhiteSpace(ColumnName), "Column Name must be valid.", "ColumnName");

            Conditions = new List<LimitCondition>();
            ResultName = "";
            ColumnName = "";
        }
    }

    /// <summary>
    /// A condition for a specific limit that must be satisfied for the limit to apply.
    /// </summary>
    public class LimitCondition : ValidatingObject
    {
        /// <summary>
        /// The result column name that this condition applies to.
        /// </summary>
        [Display("When Column", Description: "The condition value tested comes from the result column with this name.", Order: -3)]
        public string ColumnName { get; set; }
        /// <summary>
        /// The lower limit for this condition.
        /// </summary>
        [Display("Greater Than", Description: "The condition value must be above this value.", Order: -2)]
        public double LowerLimit { get; set; }
        /// <summary>
        /// The upper limit for this condition.
        /// </summary>
        [Display("Less Than", Description: "The condition value must be below this value.", Order: -1)]
        public double UpperLimit { get; set; }

        /// <summary>
        /// Returns a string describing this object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("{0} < {1} < {2}", LowerLimit, ColumnName, UpperLimit);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LimitCondition"/> class.
        /// </summary>
        public LimitCondition()
        {
            Rules.Add(() => !string.IsNullOrWhiteSpace(ColumnName), "Column Name must be valid.", "ColumnName");
        }
    }

    /// <summary>
    /// Interface to results storage plugins.
    /// </summary>
    public interface IResultStore : IResource, ITapPlugin
    {
        /// <summary>
        /// Get list of properties on entries in the database which starts with a given string.
        /// The returned properties should be ordered by their frequency of use in the dataset.
        /// </summary>
        /// <param name="scope">Only consider parameters from this scope. Could be "plan" or "step", or empty to consider all scopes.</param>
        /// <param name="group">Only consider parameters from this parameter group. Can be empty to match any group.</param>
        /// <param name="startsWith"></param>
        /// <param name="limit"></param>
        List<string> GetProperties(string scope, string group, string startsWith, int limit);

        /// <summary>
        /// Gets all <see cref="IData"/> elements which match a given search condition.
        /// </summary>
        /// <param name="cond"></param>
        /// <param name="limitsets"></param>
        /// <param name="withResults"></param>
        IEnumerable<IData> GetEntries(SearchCondition cond, List<string> limitsets, bool withResults);

        /// <summary>
        /// Tries to delete the given entries and all sub entries.
        /// </summary>
        /// <param name="entries">Entries to delete.</param>
        bool DeleteEntries(IEnumerable<IData> entries);

        /// <summary>
        /// Returns all registered limit sets.
        /// </summary>
        List<LimitSet> GetLimitSets();
        /// <summary>
        /// Adds a limit set to the results store.
        /// </summary>
        /// <param name="limitSet"></param>
        void AddLimitSet(LimitSet limitSet);
        /// <summary>
        /// Deletes a limit set from the database.
        /// </summary>
        /// <param name="Name">The name of the limit set to delete.</param>
        void DeleteLimitSet(string Name);

        /// <summary>
        /// Returns the binary data for the given objects attachment, or null if it could not be found.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="attachmentType"></param>
        byte[] GetAttachment(IData entry, AttachmentType attachmentType);
        /// <summary>
        /// Gets a list of attachments on the given object.
        /// </summary>
        /// <param name="entry"></param>
        List<AttachmentType> GetValidAttachments(IData entry);

        /// <summary>
        /// Returns the average duration of the last <paramref name="averageCount"/> step runs with similar settings.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="averageCount"></param>
        TimeSpan? GetAverageDuration(TestStepRun step, int averageCount);
        /// <summary>
        /// Returns the average duration of the last <paramref name="averageCount"/> PlanRuns runs with a similar plan.
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="averageCount"></param>
        TimeSpan? GetAverageDuration(TestPlanRun plan, int averageCount);
    }

    /// <summary>
    /// IResultStore that supports attachment streams.
    /// </summary>
    public interface IAttachmentStream : IResultStore
    {

        /// <summary>
        /// Returns a stream that can read a given attachment entry. The returned stream must be disposed.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="attachmentType"></param>
        Stream GetAttachmentStream(IData entry, AttachmentType attachmentType);
    }

    /// <summary>
    /// Interface to support result tagging in the TAP Results Viewer.
    /// </summary>
    public interface IResultTagging : IResultStore
    {
        /// <summary>
        /// Add a TestPlan parameter to a number of TestPlans.
        /// </summary>
        void AddTestplanRunParameter(IEnumerable<IData> PlanRunIDs, string Group, string ParameterName, IConvertible Value);

        /// <summary>
        /// Delete a TestPlan parameter from a number of TestPlans.
        /// </summary>
        void DeleteTestplanRunParameter(IEnumerable<IData> PlanRunIDs, string Group, string ParameterName, IConvertible Value);

        /// <summary>
        /// Get distinct TestPlan parameter values ordered by popularity (number of uses). Optionally limited to Limit values.
        /// </summary>
        IEnumerable<string> GetTestplanParameterValues(string ParameterName, int Limit = -1);
    }

    /// <summary>
    /// Interface to support querying parameter values with optional scoping.
    /// </summary>
    public interface IResultTagging2 : IResultTagging
    {

        /// <summary>
        /// Get distinct TestPlan parameter values ordered by popularity (number of uses). Optionally limited to Limit values.
        /// </summary>
        IEnumerable<string> GetTestplanParameterValues(string Scope, string Group, string ParameterName, int Limit = -1);
    }

    /// <summary>
    /// Delegate that triggers notification updates when results are added to the store.
    /// </summary>
    public delegate void ResultUpdateEvent(object Sender);

    /// <summary>
    /// Interface to add notifications when the Results Viewer gets new data.
    /// </summary>
    public interface IResultStoreNotification : IResultStore
    {
        /// <summary>
        /// Event triggered when results are added to the store.
        /// </summary>
        event ResultUpdateEvent ResultUpdated;

        /// <summary>
        /// Event triggered when TestPlan or TestStep runs are added to the store.
        /// </summary>
        event ResultUpdateEvent RunsUpdated;

        /// <summary>
        /// Enables the Updated events.
        /// </summary>
        void EnableUpdateEvents();

        /// <summary>
        /// Disables the Updated events.
        /// </summary>
        void DisableUpdateEvents();
    }

    /// <summary>
    /// When implemented along with <see cref="IResultStore"/>, this interface allows files with a given extension to be opened using your <see cref="ResultListener"/>. 
    /// It also can be used to allow the export of results to a file. 
    /// </summary>
    public interface IFileResultStore
    {
        /// <summary>
        /// Gets or sets the currently chosen file.
        /// </summary>
        string FilePath { set; get; }

        /// <summary>
        /// Default extension without the dot (.) that files must match in order to load a file's results to Results Viewer (e.g. TapResults, not .TapResults).  
        /// </summary>
        string DefaultExtension { get; }
    }
}
