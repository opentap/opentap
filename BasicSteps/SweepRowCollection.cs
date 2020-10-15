namespace OpenTap.Plugins.BasicSteps
{
    public class SweepRowCollection : VirtualCollection<SweepRow>
    {
        SweepParameterStep loop; 
        public SweepParameterStep Loop
        {
            get => loop;
            set
            {
                loop = value;
                foreach (var item in this)
                {
                    if(item != null)
                        item.Loop = loop;
                }
            }
        }

        public override void Add(SweepRow item)
        {
            if(item != null)
                item.Loop = Loop;
            base.Add(item);
        }

        public override void Insert(int index, SweepRow item)
        {   if(item != null)
              item.Loop = Loop;
            base.Insert(index, item);
        }

        public override SweepRow this[int index]
        {
            get => base[index];
            set
            {
                if(value != null)
                    value.Loop = Loop;
                base[index] = value;
            }
        }
    }
}