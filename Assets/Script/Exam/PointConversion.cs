public class PointConversion
{
    public float Convert10To4(float x)
    {
        if (x >= 9.0f) return 4.0f;    
        if (x >= 8.5f) return 3.7f;    
        if (x >= 8.0f) return 3.5f;    
        if (x >= 7.0f) return 3.0f;   
        if (x >= 6.5f) return 2.5f;    
        if (x >= 5.5f) return 2.0f;    
        if (x >= 5.0f) return 1.5f;    
        if (x >= 4.0f) return 1.0f;   
        return 0.0f;                  
    }

    public string LetterFrom10(float x)
    {
        if (x >= 9.0f) return "A+";
        if (x >= 8.5f) return "A";
        if (x >= 8.0f) return "B+";
        if (x >= 7.0f) return "B";
        if (x >= 6.5f) return "C+";
        if (x >= 5.5f) return "C";
        if (x >= 5.0f) return "D+";
        if (x >= 4.0f) return "D";
        return "F";
    }
}
