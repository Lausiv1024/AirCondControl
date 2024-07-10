using System;
using System.Data;
internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Argument is Empty");
            return;
        }
        string ReadFilePath = args[0];
        if (!File.Exists(ReadFilePath))
        {
            Console.WriteLine("File not found!");
            return;
        }

        string rawIRCode;

        using (StreamReader sr = new StreamReader(ReadFilePath))
        {
            rawIRCode = sr.ReadToEnd().Replace(Environment.NewLine, "");
        }
        string[] rawCodes = rawIRCode.Split(',');
        int[] ticks = new int[rawCodes.Length];
        for (int i = 0; i < rawCodes.Length; i++)
        {
            ticks[i] = int.Parse(rawCodes[i]) / 400;
            ticks[i] = ticks[i] == 0 ? 1 : ticks[i];
        }
        int count = 0;
        bool IsReader = false;
        bool IsDataTerminated = false;
        string decodedBinary = "";
        while (true)
        {
            if (count + 1 >= ticks.Length) break;//count + 1が配列の範囲外であれば終了

            IsReader = Isreader(ticks, count);
            if (!IsReader)
            {
                IsDataTerminated = IsTrailer(ticks, count);
                if (IsDataTerminated)
                {
                    string decodedHex = "";
                    for (int i = 0; i < decodedBinary.Length; i += 8) 
                    {
                        int a = Convert.ToInt32(decodedBinary.Substring(i, 8), 2);
                        decodedHex += a > 10 ? Convert.ToString(a, 16) : $"0{Convert.ToString(a, 16)}";
                        Console.WriteLine(a > 10 ? Convert.ToString(a, 16) : $"0{Convert.ToString(a, 16)}");
                    }
                    Console.WriteLine(decodedHex);
                    decodedBinary = string.Empty;
                    IsDataTerminated = false;
                }
                var bit = BitFromRawData(ticks, count);
                if (bit == string.Empty)
                {
                    IsDataTerminated = true;
                }
                decodedBinary += bit;
            } else
            {
                Console.WriteLine("Reader Detected");
            }
            count += 2;
        }
    }

    private static bool IsTrailer(int[] data, int index)//トレーラ(終端)検出
    {
        if (data[index] == 1 && data[index + 1] > 3)
            return true;
        return false;
    }

    private static bool Isreader(int[] data, int index)//リーダ検出
    {
        if (data.Length <= index - 1)
            return false;
        if (data[index] == 8 && data[index + 1] == 4)
            return true;
        return false;
    }
    private static string BitFromRawData(int[] data, int index)
    {
        if (data[index] == 1 && data[index + 1] == 1)
            return "0";
        if (data[index] == 1 && data[index + 1] == 3)
            return "1";
        return string.Empty;
    }
}