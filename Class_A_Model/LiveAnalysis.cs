using AdvancedHMIDrivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Class_A_Model
{
    /// <summary>
    /// 
    /// </summary>
    public class Model
    {
        //consistent values
        public int TickDuration { get; set; }
        public TrueTank HETank { get; set; }
        public TrueTank StorTank { get; set; }
        public TrueHeatExchanger HExchanger { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CurTime { get; set; }
        public DateTime PrevTime { get; set; }
        public TimeSpan TimeTracker { get; set; }
        public PLCHelpers PLC { get; set; }
        String HETankIP { get; set; }
        String BeltPressIP { get; set; }
        int HTankCap { get; set; }
        int StorTankCap { get; set; }
        int HExchangerCap { get; set; }
        public IngressTracker Tracker { get; set; }

        public int HeTankIncGallons;//the number of gallons that have entered the Heating Tank for the current tick
        public int HeTankOutGallons;//the number of gallons that have left the Heating Tank for the current tick

        //inconsistent values but need to be known by everything
        public int PHT;
        public int PHTT;

        public Model(String plant)
        {
            if (plant.Equals("COBE"))
            {
                HETankIP = Globals.COBEHeatTankIP; BeltPressIP = Globals.COBEBeltPressIP;
                HTankCap = Globals.COBEHETCap; StorTankCap = Globals.COBEStorCap; HExchangerCap = Globals.COBEHExchangerCap;
            }
            //add in more plants as necessary
            init();
            HETank.Update();

        }

        //initialize the model
        public void init()
        {
            PLC = new PLCHelpers(HETankIP, BeltPressIP);
            TickDuration = Globals.MilliSecondPerTick;
            HETank = new TrueTank(HTankCap, PLC.GetHeatTankVolume, PLC.GetHeatTankTemp);
            StorTank = new TrueTank(StorTankCap, PLC.GetHoldTankVolume, PLC.GetHoldTankTemp);
            HExchanger = new TrueHeatExchanger(HExchangerCap, PLC.GetHETempIn, PLC.GetHETempOut);
            StartTime = DateTime.Now;
            PrevTime = StartTime;
            CurTime = StartTime;
            PHT = PLC.GetHoldTankVolume(); //magic initial value
            PHTT = PLC.GetHeatTankVolume(); //magic initial value
            Tracker = new IngressTracker(PLC, PHT, PHTT);
            SetInitValues();
            Thread.Sleep(5000);

        }

        /// <summary>
        /// Gets the initial values for the process containers
        /// 
        /// </summary>
        public void SetInitValues()
        {
            //HETank - fill with atoms
            int NumAtoms = PLC.GetHeatTankVolume() * Globals.AtomsPerGallon;
            List<Atom> Atoms = new List<Atom>();
            for (int i = 0; i < NumAtoms; i++)//fill with class A atoms
            {
                Atom newAtom = new Atom();
                newAtom.MTHV = 0.0;
                newAtom.Serial = -1;//indicates initial Atom
                Atoms.Add(newAtom);
            }
            HETank.Atoms = Atoms;

            //HExchanger fill with atoms
            NumAtoms = HExchangerCap * Globals.AtomsPerGallon;
            Queue<Atom> HEAtoms = new Queue<Atom>();
            for (int i = 0; i < NumAtoms; i++)//fill with class A atoms
            {
                Atom newAtom = new Atom();
                newAtom.MTHV = 0.0;
                newAtom.Serial = -1;//indicates initial Atom
                HEAtoms.Enqueue(newAtom);
            }
            HExchanger.Atoms = HEAtoms;
        }

        public void Tick()
        {
            Tracker.Tick();
            HeTankIncGallons = Tracker.AvgIncFlowRate;//Average amount of novel material that has moved into the HETank in the past 5 min. 
            HeTankOutGallons = Tracker.AvgOutFlowRate;//we use an average amount to smooth out the rate so we have no huge jumps. Same for outgoing material
            HETank.Fill(GenerateClassBAtoms((int)HeTankIncGallons * Globals.AtomsPerGallon));
            HETank.CurVolume += HeTankIncGallons;
            HETank.Transfer(StorTank, HeTankOutGallons * Globals.AtomsPerGallon);
            HETank.CurVolume -= HeTankOutGallons;//theoretical
            StorTank.CurVolume += HeTankOutGallons;//theoretical
                                                   //compare real volumes to theoretical
            int tempVal = PLC.GetHeatTankVolume();
            if (HETank.CurVolume != tempVal)
            {
                if (HETank.Atoms.Count > tempVal * Globals.AtomsPerGallon)// if theoryVolume > realVolume
                {
                    //Calculate#of atoms to remove
                    int AtomsToRemove = (HETank.Atoms.Count - (tempVal * Globals.AtomsPerGallon));
                    //function to remove that number of atoms, prioritizing dirty atoms
                    Console.WriteLine("There are currently " + HETank.Atoms.Count + " atoms in the tank.");
                    Console.WriteLine("There are currently " + tempVal + " gallons in the tank");
                    Console.WriteLine("Remove " + AtomsToRemove + " atoms.");
                    RemoveAtomsDPriority(HETank, AtomsToRemove);
                    HETank.CurVolume = tempVal;
                }
                else if (HETank.Atoms.Count < tempVal * Globals.AtomsPerGallon)// if theoryVolume < realVolume
                {
                    //calculate atoms to add
                    int AtomsToAdd = (tempVal * Globals.AtomsPerGallon - HETank.Atoms.Count);
                    //add that many dirty atoms into the HETank
                    Console.WriteLine("There are currently " + HETank.Atoms.Count + " atoms in the tank.");
                    Console.WriteLine("There are currently " + tempVal + " gallons in the tank");
                    Console.WriteLine("Add " + AtomsToAdd + " atoms.");
                    CloneAtoms(HETank, AtomsToAdd);
                    HETank.CurVolume = tempVal;
                }
                if (HETank.CurVolume * Globals.AtomsPerGallon != (HETank.Atoms.Count))
                { Console.WriteLine("WARNING:WRONG RATIO OF ATOMS TO GALLONS " + (HETank.CurVolume * Globals.AtomsPerGallon - HETank.Atoms.Count)); }
                if (HETank.CurVolume * Globals.AtomsPerGallon == (HETank.Atoms.Count))
                { Console.WriteLine("Correct RATIO OF ATOMS TO GALLONS "); }
            }
            HExchanger.Transfer(HETank, Globals.RecirulationAmt);
            HETank.Transfer(HExchanger, Globals.RecirulationAmt);

            //calculate time differences
            PrevTime = CurTime;
            CurTime = DateTime.Now;
            TimeTracker = CurTime - PrevTime; 

            //increment MTHV for each atom in the tank
            HETank.Temperature = Helpers.ConvertFahrenheitToCelsius(PLC.GetHeatTankTemp());
            HETank.IncrementMTHV(TimeTracker.Milliseconds);

            StorTank.Temperature = Helpers.ConvertFahrenheitToCelsius(PLC.GetHoldTankTemp());
            StorTank.IncrementMTHV(TimeTracker.Milliseconds);

            HExchanger.EntryTemperature = Helpers.ConvertFahrenheitToCelsius(PLC.GetHETempIn());
            HExchanger.ExitTemperature = Helpers.ConvertFahrenheitToCelsius(PLC.GetHETempOut());
            HExchanger.IncrementMTHV(TimeTracker.Milliseconds);


            //last thing: set the pht and phtt for the next tick
            Tracker.PHT = HETank.CurVolume;
            Tracker.PHTT = StorTank.CurVolume;
            Console.WriteLine("Number of dirty Atoms: " + CountDirtyAtoms(HETank));
        }

        public void RemoveAtomsDPriority(TrueTank t, int numAtoms)
        {
            List<Atom> AtomsToRemove = new List<Atom>();
            int Count = 0;
            int DirtyAtoms = CountDirtyAtoms(t);
            while (DirtyAtoms > 0 && numAtoms > 0)
            {
                if (t.Atoms[Count].IsDirty == true) { t.Atoms.RemoveAt(Count); Count--; numAtoms--; DirtyAtoms--; }
                Count++;
            }
            t.Drain(numAtoms);
        }

        public void CloneAtoms(TrueTank t, int numAtoms)
        {
            int random;
            Atom CloneAtom;
            for (int x = 0; x < numAtoms; x++)
            {
                random = Helpers.GetRandomElement(t.Atoms.Count);
                CloneAtom = t.Atoms[random].DirtyCopy();
                t.Atoms.Add(CloneAtom);
            }
        }

        public int CountDirtyAtoms(TrueTank t)
        {
            int ReturnInt = 0;
            foreach (Atom a in t.Atoms)
            {
                if (a.IsDirty == true) { ReturnInt += 1; }
            }
            return ReturnInt;
        }

        public List<Atom> GenerateClassAAtoms(int numAtoms)
        {
            List<Atom> toBeReturned = new List<Atom>();

            for (int x = 0; x < numAtoms; x++)
            {
                Atom newAtom = new Atom();
                newAtom.MTHV = 1.0;
                newAtom.Serial = -1;
                toBeReturned.Add(newAtom);
            }

            return toBeReturned;
        }
        public List<Atom> GenerateClassBAtoms(int numAtoms)
        {
            List<Atom> toBeReturned = new List<Atom>();

            for (int x = 0; x < numAtoms; x++)
            {
                Atom newAtom = new Atom();
                toBeReturned.Add(newAtom);
            }

            return toBeReturned;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TrueTank : Tank
    {
        new public List<Atom> Atoms { get; set; }
        public int CurVolume { get; set; }
        Func<int> GetVolume;
        Func<double> GetTemp;

        public TrueTank(int capacity, Func<int> volFunc, Func<double> tempFunc)
        {
            CurVolume = 0;
            GetVolume = volFunc;
            GetTemp = tempFunc;
            CapacityInGallons = capacity;
            Atoms = new List<Atom>();
        }

        // updates the temperature and volume of the tank
        public void Update()
        {
            CurVolume = GetVolume();
            Temperature = GetTemp();
        }

        new public void IncrementMTHV(int milliseconds)
        {
            try
            {
                Atoms[0].Temperature = Temperature;
                double increment = Atoms[0].GettMTHV(milliseconds);
                Atoms.ForEach(atom =>
                {
                    atom.MTHV += increment;
                });
            }
            catch
            {
                return;
            }
        }

        new public List<Atom> Drain(int numAtoms)
        {
            List<Atom> toBeReturned = new List<Atom>();
            int random;

            for (int x = 0; x < numAtoms; x++)
            {
                random = Helpers.GetRandomElement(Atoms.Count);
                toBeReturned.Add(Atoms[random]);
                Atoms.RemoveAt(random);
            }

            return toBeReturned;
        }

        new public void Fill(List<Atom> atoms)
        {
            Atoms = Atoms.Concat(atoms).ToList();
        }

        public void Transfer(TrueHeatExchanger to, int numAtoms)
        {
            to.Fill(this.Drain(numAtoms));
        }

        public void Transfer(TrueTank to, int numAtoms)
        {
            to.Fill(this.Drain(numAtoms));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TrueHeatExchanger : HeatExchanger
    {
        new public Queue<Atom> Atoms { get; set; }
        Func<double> HeatTempIn;
        Func<double> HeatTempOut;

        public TrueHeatExchanger(int capacity, Func<double> heatTempIn, Func<double> heatTempOut)
        {
            CapacityInGallons = capacity;
            HeatTempIn = heatTempIn;
            HeatTempOut = heatTempOut;
        }

        new public void IncrementMTHV(int milliseconds)
        {
            double deltaT = ExitTemperature - EntryTemperature;

            for (int x = 0; x < Atoms.Count; x++)
            {
                //first 1/2 of heat exchanger
                if (x < Atoms.Count / 2)
                {
                    Atoms.ElementAt(x).Temperature = EntryTemperature;
                    Atoms.ElementAt(x).IncrementMTHV(milliseconds);
                }
                //heating slope in heat exchanger
                else if (x >= (Atoms.Count / 2) && x < Atoms.Count * 0.75)
                {
                    Atoms.ElementAt(x).Temperature = ExitTemperature;
                }
                //final stage of heat exchanger
                else
                {
                    Atoms.ElementAt(x).Temperature = ExitTemperature;
                    Atoms.ElementAt(x).IncrementMTHV(milliseconds);
                }
            }
        }

        new public List<Atom> Drain(int numAtoms)
        {
            List<Atom> toBeReturned = new List<Atom>();

            for (int x = 0; x < numAtoms; x++)
            {
                toBeReturned.Add(this.Atoms.Dequeue());
            }

            return toBeReturned;
        }

        new public void Fill(List<Atom> atoms)
        {
            atoms.ForEach(i => this.Atoms.Enqueue(i));
        }

        public void Transfer(TrueTank to, int numAtoms)
        {
            to.Fill(Drain(numAtoms));
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public class PLCHelpers
    {
        private const String HeatExchangerTempTagIn = "TT22A_DegF";
        private const String HeatExchangerTempTagOut = "TT22B_DegF";
        private const String HeatTankVolumeTag = "PT11";
        private const String HoldTankVolumeTag = "T008.Volume";
        private const String HoldTankTempTag = "T008.Temperature";
        private const String HeatTankTempTag1 = "TT11H";
        private const String HeatTankTempTag2 = "TT11L";
        private const String HoldTankFlowrateTag = "FT55A_Flowrate";//use beltpress ip



        private EthernetIPforCLXCom MainMicroLogixController;
        private EthernetIPforCLXCom BeltMicroLogixController;

        public PLCHelpers(String PLC_IP, String Belt_IP)
        {
            MainMicroLogixController = new EthernetIPforCLXCom();
            MainMicroLogixController.IPAddress = PLC_IP;
            BeltMicroLogixController = new EthernetIPforCLXCom();
            BeltMicroLogixController.IPAddress = Belt_IP;

        }

        //if the plc does not respond the program waits .5 seconds and tries again.
        public double GetHETempIn()
        {
            //return 165;
            double J;
            try {
                double.TryParse(MainMicroLogixController.Read(HeatExchangerTempTagIn), out J);
            }
            catch
            {
                Console.WriteLine("Warning: PLC Connection Issues");
                Thread.Sleep(500);
                return GetHETempIn();
            }
            return J;
        }

        public double GetHETempOut()
        {
            //return 185;
            double J;
            try {
                double.TryParse(MainMicroLogixController.Read(HeatExchangerTempTagOut), out J);
            }
            catch
            {
                Console.WriteLine("Warning: PLC Connection Issues");
                Thread.Sleep(500);
                return GetHETempOut();
            }
            return J;
        }

        public int GetHeatTankVolume()
        {
            //return Globals.random.Next(3700, 3800);
            int J;
            String j;
            try
            {
                j = MainMicroLogixController.Read(HeatTankVolumeTag);
            }
            catch(Exception e)
            {
                Console.WriteLine("Warning: PLC Connection Issues");
                System.Threading.Thread.Sleep(500);
                return GetHeatTankVolume();
            }
            int.TryParse(j, out J);
            return J;
        }

        public int GetHoldTankVolume()//PROBLEM CHILD
        {
            int J;
            String j;
            try
            {
                j = MainMicroLogixController.Read(HoldTankVolumeTag);
            }
            catch(Exception e)
            {
                Console.WriteLine("Warning: PLC Connection Issues");
                Thread.Sleep(500);
                return GetHoldTankVolume();
            }
            double k;
            double.TryParse(j, out k);
            J = (int)k;
            return J;
        }

        public int GetHoldTankFlowrate()
        {
            int J;
            try {
                int.TryParse(BeltMicroLogixController.Read(HoldTankFlowrateTag), out J);
            }
            catch(Exception e)
            {
                Console.WriteLine("Warning: PLC Connection Issues");
                Thread.Sleep(500);
                return GetHoldTankFlowrate();
            }
            if (J < 5) { return 0; } else { return J; }
        }

        public double GetHeatTankTemp()
        {
            double J;
            double K;
            try {
                double.TryParse(MainMicroLogixController.Read(HeatTankTempTag1), out J);
                double.TryParse(MainMicroLogixController.Read(HeatTankTempTag2), out K);
            }
            catch(Exception e)
            {
                Console.WriteLine("Warning: PLC Connection Issues");
                Thread.Sleep(500);
                return GetHeatTankTemp();
            }
            if (J >= K) { return J; } else { return K; }
        }

        public double GetHoldTankTemp()
        {
            double J;
            try {
                double.TryParse(MainMicroLogixController.Read(HoldTankTempTag), out J);
            }
            catch(Exception e)
            {
                Console.WriteLine("Warning: PLC Connection Issues");
                Thread.Sleep(500);
                return GetHoldTankTemp();
            }
            return J;
        }
    }


    /// <summary>
    /// Configuration Settings and Static Values
    /// </summary>
    public static class Globals
    {
        public static Random random = new Random();

        public const int MilliSecondPerTick = 5000;
        public const int AtomsPerGallon = 4;
        public const double MinimumIncrementTemperatureC = 50;
        public const double MinimumIncrementTemperatureF = 122;

        //COBE Values
        public const String COBEHeatTankIP = "172.16.2.100";
        public const String COBEBeltPressIP = "172.16.2.190";
        public const int COBEHETCap = 4000;
        public const int COBEStorCap = 10000;
        public const int COBEHExchangerCap = 850;
        public const int RecirulationAmt = 850;

        //FOR TEST USE ONLY
        public static int HoldTankVolume = 2000;
    }
    public class FlowRate
    {
        public double IncRate;
        public double OutRate;
        public FlowRate(double temp, double outt)
        {
            IncRate = temp;
            OutRate = outt;
        }
    }

    /// <summary>
    /// class for tracking flowrateinstances over time
    /// Designed for the heating tank not storage tank.
    /// </summary>
    public class IngressTracker
    {
        public PLCHelpers PLC;
        public FlowCalculator FCalc;
        public Queue<FlowRate> FlowInstances;
        public int AvgIncFlowRate { get; set; }
        public int AvgOutFlowRate { get; set; }
        public int PHT;
        public int PHTT;

        public IngressTracker(PLCHelpers plc, int pht, int phtt)
        {
            FlowInstances = new Queue<FlowRate>();
            AvgIncFlowRate = 0;
            AvgOutFlowRate = 0;
            PLC = plc;
            FCalc = new FlowCalculator(PLC, pht, phtt);
        }

        public void Tick()
        {
            FCalc.Tick();
            FlowRate FlowRateInstance = new FlowRate(FCalc.LHTT, FCalc.LHT);
            Push(FlowRateInstance);
            //avg flowrateinstances
            AvgIncFlowRate = 0;
            foreach (FlowRate element in FlowInstances)
            {
                AvgIncFlowRate += (int)element.IncRate;
                AvgOutFlowRate += (int)element.OutRate;
            }
            AvgIncFlowRate = AvgIncFlowRate / FlowInstances.Count;
            AvgOutFlowRate = AvgOutFlowRate / FlowInstances.Count;
        }

        public void Push(FlowRate flowRateInstance)
        {
            FlowInstances.Enqueue(flowRateInstance);
            while (FlowInstances.Count > 60)//Each tick is 5 seconds. We want 5 minutes worth of ticks. 60secs/(5 sec intervals) => 12 ticks per min * 5mins = 60 
            {
                FlowInstances.Dequeue();
            }
        }
    }

    /// <summary>
    /// Class for calculating flowrateinstance of a tick
    /// </summary>
    public class FlowCalculator
    {
        int HTT { get; set; }//Heat Treatment tank volume
        int HT { get; set; }//HoldingTankVolume
        int HTFR { get; set; }//Holding Tank Flowrate
        public double LHTT { get; set; }//Leaving Heat Treatment Tank
        public double LHT { get; set; }//Leaving Holding Tank -> This is what we're after
        public int PHT { get; set; }//previous holding tank value
        public int PHTT { get; set; }//previous heat treatment tank value
        PLCHelpers PLC;

        public FlowCalculator(PLCHelpers plc, int pht, int phtt)
        {
            PLC = plc;
            HTT = PLC.GetHeatTankVolume();
            HT = PLC.GetHoldTankVolume();
            HTFR = PLC.GetHoldTankFlowrate();
            LHTT = 0;
            LHT = 0;
            PHT = pht;
            PHTT = phtt;

        }

        //the amount of material that has left the heating tank in the past 5 seconds
        public double CalculateLHT()
        {
            LHT = HT + HTFR - PHT;
            return LHT;
        }

        //the amount of material that has entered the heating tank in the past 5 seconds 
        public double CalculateLHTT()
        {
            LHTT = HTT + LHT - PHTT;
            return LHTT;
        }

        public void SetValues()
        {
            HTT = PLC.GetHeatTankVolume();
            HT = PLC.GetHoldTankVolume();
            HTFR = PLC.GetHoldTankFlowrate();
        }

        public void Tick()
        {
            SetValues();
            CalculateLHT();//sets previous value for the next tick
            CalculateLHTT();//sets previous value for the next tick

        }
    }
}

