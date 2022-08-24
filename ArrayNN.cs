using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrayNN
{
    public float[] weights;
    public float[] biases;
    public int[] layers;
    public float[] nodeValues;
    public float learningRate = .0003f;
    private int totalWeights;
    private int totalBiases;
    private int totalNodes;
    public float[] gradient;
    private float[] zGradient;
    private float[] currentLayer;
    private float[] nextLayer;
    private float[] output;
    public bool softmax = true;

    //adam
    private bool adam = true;
    private float beta1 = .9f;
    private float beta2 = .999f;
    private float epsilon = .00000001f;
    private float[] firstMoment;
    private float[] secondMoment;
    private int updateCount = 1;

    public ArrayNN(int[] lyrs)
    {
        NeuralNetLyrs(lyrs);
        NeuralNetWAndB(new float[] { }, true);
    }

    public ArrayNN(float[] weightsAndBiases, int[] lyrs)
    {
        NeuralNetLyrs(lyrs);
        NeuralNetWAndB(weightsAndBiases, false);
    }

    public ArrayNN(string s, int[] lyrs)
    {
        NeuralNetLyrs(lyrs);

        //take the string of weights and biases and convert into a float list
        string[] numbers = s.Split(' ');
        float[] weightsAndBiases = new float[numbers.Length];
        for (int a = 0; a < numbers.Length; a++)
            weightsAndBiases[a] = float.Parse(numbers[a]);

        NeuralNetWAndB(weightsAndBiases, false);
    }

    public void NeuralNetLyrs(int[] lyrs)
    {
        //set layers
        layers = Copy(lyrs);

        //initialize arrays
        totalWeights = 0;
        for (int a = 0; a < layers.Length - 1; a++)
            totalWeights += layers[a] * layers[a + 1];
        totalBiases = 0;
        for (int a = 1; a < layers.Length; a++)
            totalBiases += layers[a];
        totalNodes = 0;
        for (int a = 0; a < layers.Length; a++)
            totalNodes += layers[a];
        nodeValues = new float[totalNodes];
        gradient = new float[totalWeights + totalBiases];
        zGradient = new float[totalNodes];
        int maxLayerNodes = Mathf.Max(layers);
        currentLayer = new float[maxLayerNodes];
        nextLayer = new float[maxLayerNodes];
        output = new float[layers[layers.Length - 1]];
    }

    public void NeuralNetWAndB(float[] wAndB, bool random)
    {
        //set weights and biases
        int weightIndex = 0;
        weights = new float[totalWeights];
        if (random)
        {
            for (int a = 0; a < layers.Length - 1; a++)
                for (int b = 0; b < layers[a + 1]; b++)
                    for (int c = 0; c < layers[a]; c++)
                    {
                        weights[weightIndex] = Random.Range(-Mathf.Sqrt(2f / layers[a]), Mathf.Sqrt(2f / layers[a]));
                        weightIndex++;
                    }
        }
        
        else
            for (int a = 0; a < weights.Length; a++)
                weights[a] = wAndB[a];

        biases = new float[totalBiases];
        for (int a = 0; a < totalBiases; a++)
            if (random) biases[a] = 0;
            else biases[a] = wAndB[a + totalWeights];
    }

    //=========================================================================================

    public float[] FeedForward(float[] inputs)
    {
        if (inputs.Length == layers[0])
        {
            int pastWeights = 0;
            int pastNodes = 0;
            int nodeCounter = layers[0];
            float sum;
            float max;

            for (int layer = 0; layer < layers.Length - 1; layer++)
            {
                //update node values for first layer because it isn't done in the feed forward step
                if(layer == 0)
                {
                    for (int a = 0; a < inputs.Length; a++)
                    {
                        currentLayer[a] = inputs[a];
                        nodeValues[a] = inputs[a];
                    }  
                }

                //loop through nodes in the next layer
                for (int a = 0; a < layers[layer + 1]; a++)
                {
                    sum = 0;

                    //loop through nodes in the current layer
                    for (int b = 0; b < layers[layer]; b++)
                        sum += currentLayer[b] * weights[pastWeights + b];

                    //apply activation to the sum and the bias
                    sum += biases[pastNodes];
                    if(layer == layers.Length - 2) nextLayer[a] = sum;
                    else nextLayer[a] = LReLU(sum);
                    nodeValues[nodeCounter] = nextLayer[a];
                    pastWeights += layers[layer];
                    pastNodes++;
                    nodeCounter++;
                }

                for(int a = 0; a < nextLayer.Length; a++)
                {
                    currentLayer[a] = nextLayer[a];
                    nextLayer[a] = 0;
                }
            }

            //current layer length is the max layer length instead of the output layer length
            for (int a = 0; a < output.Length; a++)
                output[a] = currentLayer[a];

            if (softmax)
            {
                sum = 0;
                max = Mathf.Max(output);
                for (int a = 0; a < output.Length; a++)
                {
                    output[a] = Mathf.Exp(output[a] - max);
                    sum += output[a];
                }

                for(int a = 0; a < output.Length; a++)
                {
                    output[a] /= sum;
                    nodeValues[nodeValues.Length - output.Length + a] = output[a];
                }
            }

            //return Copy(output); //makes arrays not linked but runs slower
            return output;
        }

        else
        {
            return new float[] { 0, 0, 0, 0, 0, 0 };
        }
    }

    public float[] GetGradient(float[] desiredOutput)
    {
        //a = relu(z), z = value before activation of node (weight * prior node activation + bias)
        int nodeCounter = -1;
        for (int a = 0; a < layers.Length; a++)
            nodeCounter += layers[a];
        int weightIndex = 0;
        float aDelta;
        float z;
        float zDelta;
        int highestOutputIndex = 0;
        float highestOutput = -9999999;
        for(int a = 0; a < desiredOutput.Length; a++)
        {
            if(desiredOutput[a] > highestOutput)
            {
                highestOutput = desiredOutput[a];
                highestOutputIndex = a;
            }
        }

        for (int layer = layers.Length - 1; layer >= 1; layer--)
        {
            for (int node = layers[layer] - 1; node >= 0; node--)
            {
                aDelta = 0;

                if (layer == layers.Length - 1)
                {
                    //for output find influence on cost
                    if (softmax)
                    {
                        if (node == highestOutputIndex) aDelta = nodeValues[nodeCounter] * (1 - nodeValues[nodeCounter]); //gradient with respect to policy
                        else aDelta = -nodeValues[nodeCounter] * nodeValues[nodeValues.Length - layers[layers.Length - 1] + highestOutputIndex];
                        //aDelta = desiredOutput[node] - nodeValues[nodeCounter]; //gradient with respect to log(policy)
                    }
                        
                    else 
                        aDelta = nodeValues[nodeCounter] - desiredOutput[node]; //gradient with respect to loss
                    zDelta = aDelta;
                }

                else
                {
                    for (int nextLayerNode = 0; nextLayerNode < layers[layer + 1]; nextLayerNode++)
                    {
                        //sum of delta to next layer
                        weightIndex = (nextLayerNode * layers[layer]) + node;
                        for (int a = 1; a <= layer; a++)
                            weightIndex += layers[a] * layers[a - 1];

                        aDelta += weights[weightIndex] * zGradient[nodeCounter - node + layers[layer] + nextLayerNode];
                    }

                    z = invLReLU(nodeValues[nodeCounter]);
                    zDelta = dLReLU(z) * aDelta;
                }

                zGradient[nodeCounter] = zDelta;

                //use zDelta to find weight and bias delta
                int weightGradientIndex = 0;
                for (int a = 1; a < layer; a++)
                    weightGradientIndex += layers[a] * layers[a - 1];
                weightGradientIndex += node * layers[layer - 1];

                //set weight gradient
                for (int priorNode = 0; priorNode < layers[layer - 1]; priorNode++)
                    gradient[weightGradientIndex + priorNode] = zDelta * nodeValues[nodeCounter - node - (layers[layer - 1] - priorNode)];

                //set bias gradient
                gradient[totalWeights + (nodeCounter - layers[0])] = zDelta;
                nodeCounter--;
            }
        }

        //return Copy(gradient); //makes arrays not linked but runs slower
        return gradient;
    }

    public float GetCost(float[] desiredOutput)
    {
        float cost = 0;
        if (softmax)
            for(int a = 0; a < layers[layers.Length - 1]; a++)
                cost -= desiredOutput[a] * Mathf.Log(nodeValues[nodeValues.Length - layers[layers.Length - 1] + a]);

        else
            for (int a = 0; a < layers[layers.Length - 1]; a++)
                cost += .5f * (nodeValues[nodeValues.Length - layers[layers.Length - 1] + a] - desiredOutput[a]) * (nodeValues[nodeValues.Length - layers[layers.Length - 1] + a] - desiredOutput[a]);

        return cost;
    }

    public void UpdateWAndB(float[] grad)
    {
        //gradient decent for linear output and gradient ascent for softmax output
        int direction = -1;
        if (softmax) direction = 1;

        if (adam)
        {
            //use the adam optimization algorithm
            float correctedFirstMoment = 0;
            float correctedSecondMoment = 0;
            updateCount += 1;

            if(firstMoment == null)
            {
                firstMoment = new float[grad.Length];
                secondMoment = new float[grad.Length];
            }

            for(int a = 0; a < firstMoment.Length; a++)
            {
                firstMoment[a] = beta1 * firstMoment[a] + (1 - beta1) * grad[a];
                secondMoment[a] = beta2 * secondMoment[a] + (1 - beta2) * grad[a] * grad[a];
                correctedFirstMoment = firstMoment[a] / (1 - Mathf.Pow(beta1, updateCount));
                correctedSecondMoment = secondMoment[a] / (1 - Mathf.Pow(beta2, updateCount));

                if (a < totalWeights)
                    weights[a] += direction * learningRate * correctedFirstMoment / (Mathf.Sqrt(correctedSecondMoment) + epsilon);
                else
                    biases[a - totalWeights] += direction * learningRate * correctedFirstMoment / (Mathf.Sqrt(correctedSecondMoment) + epsilon);
            }
        }

        else
        {
            //use stochastic gradient decent
            for (int a = 0; a < weights.Length; a++)
                weights[a] += grad[a] * learningRate * direction;
            for (int a = 0; a < biases.Length; a++)
                biases[a] += grad[totalWeights + a] * learningRate * direction;
        }
    }

    //===================================================================================

    float LReLU(float x)
    {
        //leaky relu function
        return Mathf.Max(0.1f * x, x);
    }

    float dLReLU(float x)
    {
        if (x >= 0)
            return 1;
        else
            return .1f;
    }

    float invLReLU(float x)
    {
        if (x >= 0)
            return x;
        else
            return 10 * x;
    }

    public float[] Copy(float[] array)
    {
        float[] copy = new float[array.Length];
        for (int a = 0; a < array.Length; a++)
            copy[a] = array[a];
        return copy;
    }

    public int[] Copy(int[] array)
    {
        int[] copy = new int[array.Length];
        for (int a = 0; a < array.Length; a++)
            copy[a] = array[a];
        return copy;
    }

    public float[] WeightsAndBiases()
    {
        //return the weights and biases in one list
        float[] wAndB = new float[weights.Length + biases.Length];
        for (int a = 0; a < weights.Length; a++)
            wAndB[a] = weights[a];
        for (int a = weights.Length; a < weights.Length + biases.Length; a++)
            wAndB[a] = biases[a - weights.Length];
        return wAndB;
    }

    public void RandomWeightsAndBiases()
    {
        for (int a = 0; a < weights.Length; a++)
            weights[a] = Random.Range(-1f, 1f);
        for (int a = 0; a < biases.Length; a++)
            biases[a] = Random.Range(-1f, 1f);
    }

    public void WAndBFromString(string s)
    {
        string[] numbers = s.Split(' ');
        for (int a = 0; a < weights.Length; a++)
            weights[a] = float.Parse(numbers[a]);
        for (int a = 0; a < totalBiases; a++)
            biases[a] = float.Parse(numbers[a + totalWeights]); ;
    }

    public string String()
    {
        string s = "";
        float[] wAndB = WeightsAndBiases();
        for (int a = 0; a < wAndB.Length; a++)
        {
            string space = " ";
            if (a == wAndB.Length - 1) space = "";
            s += wAndB[a] + space;
        }
        return s;
    }
}