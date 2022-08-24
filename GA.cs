using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GA : MonoBehaviour
{
    public bool startFresh;
    public int populationSize;
    public float mutationRate;
    private List<List<float>> currentPopulation = new List<List<float>>();
    public int generation = 0;
    public List<float> fitnesses = new List<float>();
    public string best;
    private List<float> goal = new List<float> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    // Start is called before the first frame update
    void Start()
    {
        if (startFresh)
            for (int a = 0; a < populationSize; a++) currentPopulation.Add(GetGenome());
        else
            LoadGenomes();
    }

    // Update is called once per frame
    void Update()
    {
        if(fitnesses.Count < currentPopulation.Count)
        {
            for(int a = 0; a < currentPopulation.Count; a++)
            {
                fitnesses.Add(GetGenomeFitness(currentPopulation[a]));
            }
        }

        else
        {
            //create next gen
            List<List<float>> newPopulation = new List<List<float>>();
            for (int a = 0; a < populationSize; a++)
                newPopulation.Add(Crossover(currentPopulation[Roulette(fitnesses)], currentPopulation[Roulette(fitnesses)]));
            float maxFitness = 0;
            for (int a = 0; a < fitnesses.Count; a++) maxFitness = Mathf.Max(fitnesses[a], maxFitness);
            best = PlayerPrefs.GetString("best");
            //print("Fitness = " + maxFitness + ": " + PlayerPrefs.GetString("best"));
            currentPopulation = new List<List<float>>(newPopulation);
            generation++;
            SaveGenomes();
            fitnesses.Clear();
        }
    }

    List<float> GetGenome()
    {
        List<float> list = new List<float>();
        for (int a = 0; a < 10; a++) list.Add(Random.Range(-1f, 1f));
        return list;
    }

    float GetGenomeFitness(List<float> genome)
    {
        float fitness = 10;
        for(int a = 0; a < genome.Count; a++)
            fitness -= Mathf.Abs(genome[a] - goal[a]);
        return fitness;
    }

    private List<float> Crossover(List<float> parent1, List<float> parent2)
    {
        List<float> child = new List<float>();

        for (int a = 0; a < parent1.Count; a++)
        {
            float randNum = Random.value;
            if (randNum < mutationRate)
                child.Add(Random.Range(-1f, 1f));
            else if (randNum < .5f + .5f * mutationRate)
                child.Add(parent1[a]);
            else
                child.Add(parent2[a]);
        }

        return child;
    }

    private int Roulette(List<float> fitnesses)
    {
        int index = 0;
        float sum = 0;
        float min = 0;

        for(int a = 0; a < fitnesses.Count; a++)
            min = Mathf.Min(fitnesses[a], min);
        for(int a = 0; a < fitnesses.Count; a++)
            fitnesses[a] += Mathf.Abs(min);
        for (int a = 0; a < fitnesses.Count; a++)
            sum += fitnesses[a];

        float randNum = Random.Range(0, sum);
        sum = 0;

        for (int a = 0; a < fitnesses.Count; a++)
        {
            sum += fitnesses[a];
            if (randNum < sum)
            {
                index = a;
                break;
            }
        }

        return index;
    }

    string ListToString(List<float> list)
    {
        string s = "";
        for (int a = 0; a < list.Count; a++)
        {
            string space = "";
            if (a != list.Count - 1) space = " ";
            s += list[a] + space;
        }
        return s;
    }

    List<float> StringToList(string s)
    {
        string[] numbers = s.Split(' ');
        List<float> list = new List<float>();
        for (int a = 0; a < numbers.Length; a++)
            list.Add(float.Parse(numbers[a]));
        return list;
    }

    void SaveGenomes()
    {
        for (int a = 0; a < currentPopulation.Count; a++)
            PlayerPrefs.SetString(a.ToString(), ListToString(currentPopulation[a]));

        //save the best
        int index = 0;
        float bestFitness = 0;
        for (int a = 0; a < fitnesses.Count; a++)
        {
            if (fitnesses[a] > bestFitness)
            {
                index = a;
                bestFitness = fitnesses[a];
            }
        }

        PlayerPrefs.SetString("best", ListToString(currentPopulation[index]));
        PlayerPrefs.SetInt("gen", generation);
    }

    void ClearGenomes()
    {
        PlayerPrefs.DeleteAll();
    }

    void LoadGenomes()
    {
        if (currentPopulation.Count > 0) currentPopulation.Clear();
            
        for (int a = 0; a < populationSize; a++)
            currentPopulation.Add(StringToList(PlayerPrefs.GetString(a.ToString())));

        generation = PlayerPrefs.GetInt("gen", 0);
    }
}
