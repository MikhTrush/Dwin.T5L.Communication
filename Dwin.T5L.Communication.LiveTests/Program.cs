using System.IO.Ports;

namespace Dwin.T5L.Communication.LiveTests;

internal class Program
{
    static DwinDisplayNoMem dwin;

    static void Main(string[] args)
    {
        var s = SerialPort.GetPortNames()[0];

        var sp = new SerialPort("COM18", 115200);

        Console.WriteLine("opened prt" + sp.BaudRate);

        dwin = new DwinDisplayNoMem(sp, 50, 2);

        dwin.ChangeDisplayBaudRate(806400);

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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                //throw;
            }
            Thread.Sleep(1);

            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
            {
                break;
            }
        }

        dwin.ChangeDisplayBaudRate(115200);
    }
}
