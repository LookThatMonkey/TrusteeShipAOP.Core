# TrusteeShipAOP.Core
​
Need to cite TrusteeShipAOP.Core.X64.dll or TrusteeShipAOP.Core.X86.dll

After that, you need to register in program.main

static void Main()
        {
            TrusteeShipAOP.Core.Environment.Initial("A67D94BB7C436944E1BED10A58A960030B460998C862E0A54302250F109DC70EF60585FCB60E67A634D7F7603F19885B9303A2336CD0E408", out _);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

Set up the interceptor after that

class DemoPropertyAspectAttribute : MPAspectAttribute
    {
        public override void OnEntry(object sender, AspectEventArgs args)
        {
            args. Name = "OnEntry " + args. Name;
        }

public override void OnExit(object sender, AspectEventArgs args)
        {
            args. Name = "OnExit " + args. Name;
        }
    }

Then the class adds attributes, but note that classes also have attributes

[ClassAspect]
    public class Class1
    {
        static int s1 = 0;

[DemoPropertyAspect]
        public int aabbc { get; set;} = 0;
        protected int ia = 0;
        int ic = 11;
        int ib = 22;
        [DemoMethodAspect]
        public int Add(int i1, int i2)
        {
            s1 = 2;
            ib = s1;
            aa(0, 0);
            aa2(0, 0);
            aabbc = 1;
            return i1 + i2;
        }

private int aa(int i1, float f2)
        {
            return 123;
        }

private static int aa2(int i1, float f2)
        {
            return 123;
        }
    }

There is no difference between the use of regular classes

Class1 Class1 = new Class1();
            Class1.Add(1,1);
            Class1.aabbc++;

At this point, you will be able to see the interceptor execute

You only need to set the features to achieve the effect of AOP, no other special writing is required, and it can also be easily data-driven.
