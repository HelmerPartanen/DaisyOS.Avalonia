using System;

class Program
{
    static void Main()
    {
        Color seed = new Color(255, 30, 80, 150);
        Color baseDark = new Color(255, 18, 18, 18);
        Color baseLight = new Color(255, 244, 245, 247);
        
        Console.WriteLine("Dark 10%: " + Blend(baseDark, seed, 0.10));
        Console.WriteLine("Dark 15%: " + Blend(baseDark, seed, 0.15));
        Console.WriteLine("Dark 20%: " + Blend(baseDark, seed, 0.20));
        Console.WriteLine("Dark 25%: " + Blend(baseDark, seed, 0.25));
        Console.WriteLine("Dark 30%: " + Blend(baseDark, seed, 0.30));
        
        Console.WriteLine("Light 4%: " + Blend(baseLight, seed, 0.04));
    }
    
    struct Color {
        public byte A, R, G, B;
        public Color(byte a, byte r, byte g, byte b) { A=a; R=r; G=g; B=b; }
        public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
    }
    
    static Color Blend(Color background, Color foreground, double amount) =>
        new Color(
            255,
            (byte)Math.Round(background.R + ((foreground.R - background.R) * amount)),
            (byte)Math.Round(background.G + ((foreground.G - background.G) * amount)),
            (byte)Math.Round(background.B + ((foreground.B - background.B) * amount)));
}
