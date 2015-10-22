using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Class_A_Model
{
    public interface ProcessContainer
    {
        int CapacityInGallons { get; set; }
        void Fill(List<Atom> atoms);
        List<Atom> Drain(int numAtoms);
        void Transfer(ProcessContainer to, int numAtoms);
    }

    public class Atom
    {
        public double MTHV { get; set; }
        public int Serial { get; set; }
        public DateTime CreationTime { get; set; }
        public double Temperature { get; set; }
        public bool IsDirty;

        public Atom()
        {
            MTHV = 0;
            Serial = Helpers.GetSerial();
            CreationTime = Helpers.GetSimulationTime();
            Temperature = 0;
            IsDirty = false;
        }

        public Atom DirtyCopy()
        {
            Atom ReturnAtom = (Atom)this.MemberwiseClone();
            ReturnAtom.IsDirty = true;
            return ReturnAtom;
        }

        public void IncrementMTHV(int milliseconds)
        {
            MTHV += (((double)milliseconds) / 86400000d) / (130000000d / Math.Pow(10, 0.14 * Temperature));
        }

        public double GettMTHV(int milliseconds)
        {
           
            double temp1 = (double)milliseconds / 86400000d;
            double temp2 = 130000000d / Math.Pow(10, 0.14 * Temperature);
            double returnVal = temp1 / temp2;
            return returnVal;
        }

        public void IncrementMTHV(int milliseconds, double temperature)
        {
            MTHV += (((double)milliseconds) / 86400000d) / (130000000d / Math.Pow(10, 0.14 * temperature));
        }

        public double GettMTHV(int milliseconds, double temperature)
        {
            return (((double)milliseconds) / 86400000d) / (130000000d / Math.Pow(10, 0.14 * temperature));
        }
    }

    public class Tank : ProcessContainer
    {
        public List<Atom> Atoms { get; set; }
        public double Temperature { get; set; }
        public int CapacityInGallons { get; set; }

        public Tank()
        {
            Atoms = new List<Atom>();
            Temperature = 0;
            CapacityInGallons = 0;
        }

        public void IncrementMTHV(int milliseconds)
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

        public List<Atom> Drain(int numAtoms)
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

        public void Fill(List<Atom> atoms)
        {
            Atoms = Atoms.Concat(atoms).ToList();
        }

        public void Transfer(ProcessContainer to, int numAtoms)
        {
            to.Fill(Drain(numAtoms));
        }
    }

    public class HeatExchanger : ProcessContainer
    {
        public Queue<Atom> Atoms { get; set; }
        public double EntryTemperature { get; set; }
        public double ExitTemperature { get; set; }
        public double FlowRate { get; set; }
        public int CapacityInGallons { get; set; }

        public HeatExchanger()
        {
            Atoms = new Queue<Atom>();
            EntryTemperature = 0;
            ExitTemperature = 0;
            FlowRate = 0;
            CapacityInGallons = 0;
        }

        public void IncrementMTHV(int milliseconds)
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

        public List<Atom> Drain(int numAtoms)
        {
            List<Atom> toBeReturned = new List<Atom>();

            for (int x = 0; x < numAtoms; x++)
            {
                toBeReturned.Add(Atoms.Dequeue());
            }

            return toBeReturned;
        }

        public void Fill(List<Atom> atoms)
        {
            atoms.ForEach(i => Atoms.Enqueue(i));
        }

        public void Transfer(ProcessContainer to, int numAtoms)
        {
            to.Fill(Drain(numAtoms));
        }
    }

    public static class Helpers
    {
        static int serialTracker = 0;
        static DateTime simulationTime = DateTime.Now;

        static Random random = new Random();

        public static int GetSerial()
        {
            return serialTracker++;
        }

        public static DateTime GetNow()
        {
            return DateTime.Now;
        }

        public static int GetRandomElement(int count)
        {
            return random.Next(0, count);
        }

        public static DateTime GetSimulationTime()
        {
            return simulationTime;
        }

        public static void TickSimulationTime(int millisPerTick)
        {
            simulationTime = simulationTime.AddMilliseconds(millisPerTick);
        }

        public static double ConvertCelsiusToFahrenheit(double c)
        {
            return ((9.0 / 5.0) * c) + 32;
        }

        public static double ConvertFahrenheitToCelsius(double f)
        {
            return (5.0 / 9.0) * (f - 32);
        }
    }
}
