using System.Collections.Generic;
using UnityEngine;
public class Inventory : MonoBehaviour {
        
    public levelData LevelData;
    [SerializeField] private int maxProb = 0;
    private int pullMin = 0;
    private int pullMax = 5;

    [Header("Pitty Data")]
    public int retryCount = 0;
    public int pittyThreshold = 10;
    public int rarenessThreshold = 50;

    [Header("Track Piece Arrays")]
    public GameObject[] playerSelection = new GameObject[3];
    public List<CellData> completeTrackSelection = new List<CellData>();

    void Start() {
        trackDataSync();
        trackPiecePull(pullMin, pullMax);
    }

    void Update() {

    }

    public void trackDataSync() {
        completeTrackSelection.Clear();
        maxProb = 0;
        for (int i = 0; i < LevelData.trackCells.Count; i++) {
            CellData source = LevelData.trackCells[i];
            CellData copy = new CellData {
                trackPiece = source.trackPiece,
                probability = source.probability
            };
            completeTrackSelection.Add(copy);
            if (source.probability > maxProb) {
                maxProb = source.probability;
            }
        }
        pullMax = LevelData.trackCells.Count;
        Debug.Log("maxProb: " + maxProb);
    }

    void trackPiecePull(int min, int max) {
        if (completeTrackSelection.Count < 3) {
            Debug.LogError("Inventory: need at least 3 track pieces in completeTrackSelection.");
            return;
        }
        if (maxProb <= 0) {
            Debug.LogError("Inventory: maxProb is 0 — no track piece has a usable probability value.");
            return;
        }

        if (retryCount % pittyThreshold == 0) {
            for (int i = 0; i < completeTrackSelection.Count; i++) {
                if (completeTrackSelection[i].probability < rarenessThreshold) {
                    completeTrackSelection[i].probability += 10;
                }
            }
            retryCount -= 10;
        }

        int[] pieces = uniqueRandNum(min, max);
        if (pieces == null) return;

        for (int i = 0; i < 3; i++) {
            GameObject instance = Instantiate(completeTrackSelection[pieces[i]].trackPiece);
            instance.name += $" #{i}"; // Clearer Naming for testing with few example track pieces
            playerSelection[i] = instance;
        }
    }

    public int[] uniqueRandNum(int min, int max) {
        if ((max) - min < 3) {
            max += (3 - (max - min));
        }
        HashSet<int> uniqueCheck = new HashSet<int>();
        int safetyCounter = 0;
        while (uniqueCheck.Count < 3) {
            if (++safetyCounter > 10000) {
                Debug.LogError("uniqueRandNum: couldn't find 3 unique qualifying pieces — check probability values.");
                return null;
            }
            int tmp = Random.Range(min, max);
            int roll = Random.Range(0, maxProb + 1);
            //Debug.Log("tmp: "  + tmp + " Roll: " + roll);
            if (roll < completeTrackSelection[tmp].probability) {
                uniqueCheck.Add(tmp);
            }
        }
        int[] result = new int[3];
        uniqueCheck.CopyTo(result);
        return result; // Returns array of random track piece indexes
    }
}