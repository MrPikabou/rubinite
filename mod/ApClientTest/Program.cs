using System;
using System.Threading;
using RubiniteAP;

internal static class Program
{
    private static int _items;

    private static void Main()
    {
        var client = new ApClient();
        client.Log += message => Console.WriteLine("LOG " + message);
        client.ConnectionChanged += (connected, message) => Console.WriteLine("CONN " + connected + " " + message);
        client.Connected += info =>
        {
            Console.WriteLine("CONNECTED slot=" + info.Slot + " seed=" + info.SeedName +
                              " checked=" + info.CheckedLocations.Count +
                              " slotdata=" + (info.SlotData != null));
            if (info.SlotData != null)
                Console.WriteLine("SLOTDATA " + info.SlotData.ToString(Newtonsoft.Json.Formatting.None).Substring(0, 80));
        };
        client.ItemsReceived += (index, list) =>
        {
            _items += list.Count;
            Console.WriteLine("ITEMS index=" + index + " count=" + list.Count);
            for (var i = 0; i < Math.Min(3, list.Count); i++)
                Console.WriteLine("  item " + list[i].ItemId + " player=" + list[i].Player);
        };

        client.Connect("127.0.0.1", 38283, "Solo", "", Guid.NewGuid().ToString());
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            client.Pump();
            Thread.Sleep(50);
        }

        Console.WriteLine("TOTAL ITEMS " + _items);
        client.SendLocationChecks(new long[] { 71000 });
        Thread.Sleep(300);
        client.SendGoal();
        Thread.Sleep(700);
        client.Pump();
        client.Dispose();
        Console.WriteLine("DONE");
    }
}
