namespace OpenTap
{
    class MixinMenuModelFactory : ITypeMenuModelFactory, IMenuModelFactory
    {
        public ITypeMenuModel CreateModel(ITypeData type)
        {
            if (type.DescendsTo(typeof(ITestStepParent)) || type.DescendsTo(typeof(IResource)))
                return new MixinMenuModel(type);
            return null;
        }
        public IMenuModel CreateModel(IMemberData member)
        {
            switch (member)
            {
                case MixinMemberData md:
                {
                    return new MixinMemberMenuModel(md);
                }
                case EmbeddedMemberData emb:
                {
                    return CreateModel(emb.OwnerMember);    
                }
            }
            return null;
        }
    }
}
