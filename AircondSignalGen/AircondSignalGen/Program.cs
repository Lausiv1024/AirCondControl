// See https://aka.ms/new-console-template for more information
using AircondSignalGen;
using Newtonsoft.Json;

Console.WriteLine("Hello, World!");

var np081 = new NP081()
{
    Power = true,
    OperationMode = OperationMode.COOLING,
    Degrees = 27,
};
var sig = Convert.ToHexString(np081.GetCurrentSignal());
Console.WriteLine(sig);
Console.WriteLine(JsonConvert.SerializeObject(np081));
