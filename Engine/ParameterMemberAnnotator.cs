using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    class ParameterMemberAnnotator : IAnnotator
    {
        public double Priority => 20;
        class SubAvailable : IAvailableValuesAnnotation
        {
            public IEnumerable AvailableValues => sub.Get<IAvailableValuesAnnotation>().AvailableValues;

            AnnotationCollection sub;
            public SubAvailable(AnnotationCollection subcol) => sub = subcol;
        }

        class SubSuggested : ISuggestedValuesAnnotation
        {
            AnnotationCollection subcol;
            public SubSuggested(AnnotationCollection subcol) => this.subcol = subcol;
            public IEnumerable SuggestedValues => subcol.Get<ISuggestedValuesAnnotation>().SuggestedValues;
        }

        class SubAccess : IAccessAnnotation
        {
            private AnnotationCollection sub;

            public bool IsReadOnly => sub.Get<IAccessAnnotation>().IsReadOnly;
            public bool IsVisible => sub.Get<IAccessAnnotation>().IsVisible;
            public SubAccess(AnnotationCollection sub) => this.sub = sub;
        }

        class SubMember : IOwnedAnnotation
        {
            readonly AnnotationCollection sub;
            readonly bool bigList;
            readonly IParameterMemberData param;

            public SubMember(AnnotationCollection sub, bool bigList, IParameterMemberData param)
            {
                this.sub = sub;
                this.bigList = bigList;
                this.param = param;
            }
            public void Read(object source)
            {
                sub.Read();
            }

            public void Write(object source)
            {
                sub.Write();
                if (bigList) // for big lists, we try to copy the values.
                    param.SetValue(source, param.GetValue(source));
            }
        }

        // Since having multiple parameter member annotations nested, creates multiple levels of parameter member annotation
        // things can become incredibly slow when this happens. 
        // it is much faster if we can just crush all the multiple levels of IParameterMemberData into on layer
        // of no-IParameterMemberData.
        // This should be safe since IParameterMemberDatas generally does not have any information connected to them
        // that are not already present on the sub-MemberData.
        static List<(object, IMemberData)> GetRootMembers(IParameterMemberData member)
        {
            List<(object, IMemberData)> nonParameters = new List<(object, IMemberData)>();
            Stack<IParameterMemberData> workQueue = new Stack<IParameterMemberData>();
            workQueue.Push(member);
            while (workQueue.Count > 0)
            {
                var mem = workQueue.Pop();
                foreach (var (s, m) in mem.ParameterizedMembers)
                {
                    if (m is IParameterMemberData p)
                        workQueue.Push(p);
                    else
                        nonParameters.Add((s, m));
                }
            }

            return nonParameters;
        }

        public void Annotate(AnnotationCollection annotation)
        {
            var member = annotation.Get<IMemberAnnotation>()?.Member as IParameterMemberData;
            if (member == null) return;
            // if the list is really huge, the next operations will be quite complex.
            // if we assume that the elements are simple in nature, we can regain that performance
            // by only merging a subset of them and then just copy the values for the rest of the objects.
            bool bigList = member.ParameterizedMembers.Count() > 100;
            var parameterizedMembers = GetRootMembers(member);
            
            var items = parameterizedMembers.Select(x => x.Item1).Take(100).ToArray();
            var cache = annotation.Get<AnnotationCache>(true);
            if (cache == null)
            {
                cache = new AnnotationCache();
                annotation.ParentAnnotation.Add(cache);
            }
            var subannotation = cache?.Annotate(items) ?? AnnotationCollection.Annotate(items.Length == 1 ? items[0] : items);
            annotation.Add(new SubMember(subannotation, bigList, member));
            var subMembers = subannotation.Get<IMembersAnnotation>();
            var firstmem = parameterizedMembers.First().Item2;
            var thismember = subMembers.Members.FirstOrDefault(x => x.Get<IMemberAnnotation>().Member == firstmem);
            if (thismember == null) return;

            annotation.RemoveType<IAccessAnnotation>();
            annotation.Add(new SubAccess(thismember));

            IAvailableValuesAnnotation avail = annotation.Get<IAvailableValuesAnnotation>();
            if (avail != null) annotation.Add(new SubAvailable(thismember));

            ISuggestedValuesAnnotation suggested = annotation.Get<ISuggestedValuesAnnotation>();
            if (suggested != null) annotation.Add(new SubSuggested(thismember));
        }
    }
}