using UnityEngine;

[System.Serializable]
public class SessionData
{
    public string Day;
    public int Slot;
}

[System.Serializable]
public class SubjectData
{
    public string Name;
    [Min(0)]
    public int MaxAbsences;
    public SessionData[] Sessions;
}

[CreateAssetMenu(fileName = "SemesterConfig", menuName = "Configs/SemesterConfig")]
public class SemesterConfig : ScriptableObject
{
    public int Semester;
    public int Weeks;
    public SubjectData[] Subjects;
}
