using UnityEngine;

public interface IAlgorithm
{
    // Called by AlgorithmManager when the algorithm should start
    void StartAlgorithm();
    
    //Called by AlgorithmManager when the algorithm should stop and clean up
    void StopAlgorithm();
}
