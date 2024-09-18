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

# Solve the problem that the underlying platform software is over-expanded and ultimately unmaintainable
Headache platform maintenance

I have maintained several small and medium-sized platform software, and in the end, they all cannot be maintained.

I have roughly summarized the reasons for the maintenance (there may be some mistakes, please don't criticize).

1. The platform architecture cannot achieve unified development rules in the middle and late stages. Any platform software is carefully structured in the early stages of development. However, with the influence of factors such as the increasing complexity of business logic and the increasing urgency of tasks, the rules initially set have to be broken again and again.
2. The bottom layer of the platform architecture will expand infinitely in the middle and late stages, which will eventually lead to excessive maintenance costs. No software can meet all future business needs from the beginning of design, even if it does not change in the next five years. There will be various events, interfaces and functions that need to be continuously expanded to adapt to the increasingly rich needs.
3. It is difficult to ensure that the development of the platform architecture and the subsequent maintenance are done by the same group of people. Whether it is due to promotion factors or job changes, the maintenance of a platform software will definitely go through several groups of people. And the platform software will reflect the thoughts of the architect. This is destined to be the cognition of the dominant person, and the thoughts will definitely be reflected in the platform software. A platform software with several ideas will never be perfect.

Discovery
The above reasons are all caused by one factor, that is, business requirements are changing, which causes the underlying layer to change and need maintenance.
Business requirements change cannot be changed, this is inevitable. So if you want to solve these problems, you can only work from the bottom.
Then the question is, how to keep the bottom layer unchanged? My experience tells me that everything can be atomized, and you can keep splitting it down and keep looking until you find the smallest atomic thing that does not change. If you find that you still cannot meet the requirements, then you must be in the wrong direction. This world will definitely give you a hint, so go and see more.
Later, I suddenly thought that technology is advancing, and programming languages ​​have undergone tremendous changes in both types and syntax. But what has not changed is that storage devices can only store 1 0. The CPU can only perform 1 0 operations.
The TCP\IP communication protocol has not changed, but it has been able to adapt for so many years. Even in the future, I will think that storage, CPU, TCP\IP will definitely not change.

Thinking
Why can memory, CPU, TCP\IP adapt to everything?
I can only realize two points, but these two points gave me infinite inspiration.
1. These are rules that must be followed. Over time, these rules become a matter of course. Anything that does not meet these rules, whether advanced or not, is judged as non-compliant or non-standard. Although it is a bit overbearing, it does allow the software system to run perfectly for a long time.
2. These do not involve specific business logic. All of these do not involve specific business logic and data. It's just a black box with input and output.

New platform software concept
1. The platform does not involve any business logic, unit functions, services, triggers, etc., and is only responsible for transfer.
2. The platform's services are provided by plug-ins, and there is only one way to register services.
3. The platform's trigger is provided by plug-ins, and there is only one way to trigger.

OK
A few years ago, I developed two things to serve this concept.
TrusteeShipAOP.Core
https://github.com/LookThatMonkey/TrusteeShipAOP.Core
https://github.com/LookThatMonkey/TrusteeShipAOP.Core.Demo
The function of this thing is to reduce the amount of code development while automatically helping the class register events of property and class value changes.
In this way, the entire platform becomes a data-driven platform architecture.

SignalSolt.NET
https://github.com/LookThatMonkey/SignalSolt.NET
https://github.com/LookThatMonkey/SignalSolt.NET.Demo
This is another thing, that is, the concept of signal slots required for event registration, which comes from QT. The main idea is to create events through strings and then trigger events through strings. This idea directly reminds me of Webservice, and I can directly achieve almost complete decoupling of code on CS.
At present, I have used this concept to develop software platforms in medium-sized projects, and the software platform has been running smoothly for more than a year and is very stable (of course, business logic bugs are not included in this statistics).
