using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Représente un nœud dans un arbre de patrouille.
/// Chaque nœud contient une position et des chemins vers d'autres nœuds.
/// </summary>
[System.Serializable]
public class PatrolNode
{
    public Transform point; //Chaque "nœud" de patrouille contient une position (Transform)  
    public List<PatrolNode> nextNodes = new List<PatrolNode>();

    //ainsi qu'une liste de nœuds suivants possibles. L'ennemi peut ainsi se déplacer de façon 
    //*   non-linéaire en choisissant dynamiquement le prochain point à atteindre,
    //*   ce qui permet un comportement plus intelligent et varié.
    public PatrolNode(Transform point)
    {
        this.point = point;
    }

    /// <summary>
    /// Choisit aléatoirement un des nœuds suivants.
    /// </summary>
    public PatrolNode GetNextNode()
    {
        if (nextNodes.Count == 0) return null;
        return nextNodes[Random.Range(0, nextNodes.Count)];
    }
}

//Ce script définit la classe PatrolNode utilisée pour créer une structure 
 //*   de données en forme d’arbre ou de graphe représentant un système de patrouille.
    