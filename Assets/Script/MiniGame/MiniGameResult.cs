namespace HHH.MiniGame
{
    [System.Serializable]
    public struct MiniGameResult
    {
        public string MiniGameId;
        public bool Success;
        public int Score;
        public float TimeSpent;
        public string ExtraJson; // tùy chọn ghi log chi tiết
    }
}