using System.IO.Ports;

namespace Dwin.T5L.Communication.LiveTests;

internal class Program
{
    static DwinDisplayNoMem dwin;

    static void Main(string[] args)
    {
        var s = SerialPort.GetPortNames()[0];

        var sp = new SerialPort("/dev/ttyS2", 115200);

        Console.WriteLine("opened prt" + sp.BaudRate);

        dwin = new DwinDisplayNoMem(sp, 50, 2);

        dwin.ClearMemory(0x5000, 0x0fff);




        //foreach (var i in Enumerable.Range(0, 0xFF))
        //{
        //    dwin.GoToPage((ushort)i);
        //}

        dwin.GoToPage(0x0);
        Thread.Sleep(1);
        try { dwin.GoToPage(4); }
        catch { }

        while (true)
        {
            try
            {
                var values = dwin.ReadVariablesRange(0x5000, 5);

                foreach (var v in values)
                {
                    Console.Write($"{v} ");
                    
                }

                Console.WriteLine();

                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                //throw;
            }
        }
    }
}
