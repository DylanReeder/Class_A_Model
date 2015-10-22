using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Class_A_Model
{
    class MainTest
    {
        static void Main(string[] args)
        {
            int count = 0;
            Model myModel = new Model("COBE");
            while (true)
            {
                myModel.Tick();
                count++;
                Console.WriteLine("MTHV (T011): {0}", myModel.HETank.Atoms.Average(i => i.MTHV));
                if (count % 100 ==0)
                {
                    double dirtyAtoms = myModel.CountDirtyAtoms(myModel.HETank);
                    double totalAtoms = myModel.HETank.Atoms.Count;
                    double prctDirty = dirtyAtoms/totalAtoms;

                    Console.WriteLine(prctDirty *100 + "% of the atoms in the heating tank are dirty");
                }
                System.Threading.Thread.Sleep(5000);
                Console.WriteLine("--------------------------------------------------------------");
            }
        }
    }
}
