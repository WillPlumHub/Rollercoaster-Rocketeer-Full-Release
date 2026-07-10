using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "levelData")]
public class levelData : ScriptableObject {
    public string Name;
    public string Difficulty;

    public List<CellData> trackCells = new List<CellData>();
}