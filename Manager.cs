using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Manager : MonoBehaviour
{
    public GameObject gameReference;
    public AllQueens game;
    private Board currentBoard;

    public bool p1Bot;
    public bool p2Bot;

    private GameObject pieceTouched = null;
    private Vector2 fromPos = Vector2.zero;
    private Vector2 toPos = Vector2.zero;

    private Board simulatedBoard;
    private float timer;
    private int simulationSpeed = 100;
    public List<float> moveWins = new List<float>();
    public List<float> moveSimulations = new List<float>();

    void Start()
    {
        game = gameReference.GetComponent<AllQueens>();
        currentBoard = new Board(this);
        currentBoard.FindPossibleMoves();
        game.SpawnBoard();
        if(game.movePieces)
            game.SpawnPieces(currentBoard);
    }

    void Update()
    {
        if(currentBoard.finished)
            ResetBoard();

        else
        {
            if(currentBoard.p1 && !p1Bot || !currentBoard.p1 && !p2Bot)
                UserInput();
            else
                MCTS();
        }

        if (Input.GetKey(KeyCode.Escape))
            Application.Quit();
    }

    void UserInput()
    {
        Vector2 pointerPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 snappedPointerPos = new Vector3(Mathf.Round(pointerPos.x), Mathf.Round(pointerPos.y), 0);

        if (game.movePieces)
        {
            if (pieceTouched == null)
            {
                if (Input.GetMouseButton(0))
                {
                    for (int a = 0; a < GameObject.FindGameObjectsWithTag("Pieces").Length; a++)
                    {
                        GameObject piece = GameObject.FindGameObjectsWithTag("Pieces")[a];
                        int clickedState = game.CordToState(snappedPointerPos);
                        bool withInBounds = clickedState >= 0 && clickedState < currentBoard.boardState.Count;
                        bool choseRightSide = false;
                        if(withInBounds)
                            choseRightSide = (currentBoard.boardState[clickedState] == 1 && currentBoard.p1) || (currentBoard.boardState[clickedState] == -1 && !currentBoard.p1);
                        if (withInBounds && choseRightSide && snappedPointerPos == piece.transform.position)
                        {
                            pieceTouched = piece;
                            pieceTouched.GetComponent<SpriteRenderer>().sortingOrder = 1;
                            fromPos = snappedPointerPos;
                        }
                    }
                }       
            }

            else
            {
                if (Input.GetMouseButton(0))
                    pieceTouched.transform.position = pointerPos;

                else
                {
                    //if move valid, make move and display
                    bool valid = false;
                    int moveIndex = 0;
                    toPos = snappedPointerPos;

                    for (int a = 0; a < currentBoard.possibleMoves.Count; a++)
                    {
                        if (currentBoard.possibleMoves[a].from == game.CordToState(fromPos) && currentBoard.possibleMoves[a].to == game.CordToState(toPos))
                        {
                            valid = true;
                            moveIndex = a;
                        }
                    }

                    if (valid)
                    {
                        currentBoard = currentBoard.MakeMove(currentBoard.possibleMoves[moveIndex]);
                        currentBoard.FindPossibleMoves();
                        currentBoard.Evaluate();
                        game.SpawnPieces(currentBoard);
                    }

                    else
                    {
                        game.SpawnPieces(currentBoard);
                        pieceTouched.GetComponent<SpriteRenderer>().sortingOrder = 0;
                        pieceTouched = null;
                        fromPos = Vector2.zero;
                        toPos = Vector2.zero;
                    }
                }
            }
        }

        else
        {
            if (pieceTouched == null)
            {
                if (currentBoard.p1) pieceTouched = Instantiate(game.pieces[0], pointerPos, Quaternion.identity);
                else pieceTouched = Instantiate(game.pieces[1], pointerPos, Quaternion.identity);
                Color color = pieceTouched.GetComponent<SpriteRenderer>().color;
                pieceTouched.GetComponent<SpriteRenderer>().color = new Color(color.r, color.g, color.b, .5f);
            }

            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    bool valid = false;
                    int moveIndex = 0;
                    toPos = snappedPointerPos;
                    for (int a = 0; a < currentBoard.possibleMoves.Count; a++)
                    {
                        if (currentBoard.possibleMoves[a].to == game.CordToState(toPos))
                        {
                            valid = true;
                            moveIndex = a;
                        }
                    }

                    if (valid)
                    {
                        currentBoard = currentBoard.MakeMove(currentBoard.possibleMoves[moveIndex]);
                        currentBoard.FindPossibleMoves();
                        currentBoard.Evaluate();
                        game.SpawnPieces(currentBoard);
                        Destroy(pieceTouched);
                    }

                    else
                    {
                        fromPos = Vector2.zero;
                        toPos = Vector2.zero;
                    }
                }

                else
                {
                    pieceTouched.transform.position = pointerPos;
                }
            }
        }
    }

    void MCTS()
    {
        if(simulatedBoard == null)
            simulatedBoard = new Board(this, currentBoard.boardState, currentBoard.p1);
        int highestSimulations = 0;
        for (int a = 0; a < simulatedBoard.nextBoards.Count; a++)
            highestSimulations = Mathf.Max(highestSimulations, simulatedBoard.nextBoards[a].simulations);

        if (highestSimulations > game.simulationLimit)
        {
            int index = 0;
            highestSimulations = 0;
            for(int a = 0; a < simulatedBoard.nextBoards.Count; a++)
            {
                if(simulatedBoard.nextBoards[a].simulations > highestSimulations)
                {
                    index = a;
                    highestSimulations = simulatedBoard.nextBoards[a].simulations;
                }
            }

            simulatedBoard = null;
            currentBoard = currentBoard.MakeMove(currentBoard.possibleMoves[index]);
            currentBoard.FindPossibleMoves();
            currentBoard.Evaluate();
            game.SpawnPieces(currentBoard);
        }

        else
        {
            for(int num = 0; num < simulationSpeed; num++)
            {
                //select move
                List<Board> path = new List<Board> { simulatedBoard };
                while (path[path.Count - 1].nextBoards.Count > 0 && !path[path.Count - 1].finished)
                {
                    List<int> highestIndecies = new List<int>();
                    float highestUcb = -999999;
                    for (int a = 0; a < path[path.Count - 1].nextBoards.Count; a++)
                    {
                        Board b = path[path.Count - 1].nextBoards[a];
                        float ucb = (b.wins / b.simulations) + (b.c * Mathf.Sqrt(Mathf.Log(path[path.Count - 1].simulations) / b.simulations));
                        if (ucb > highestUcb)
                        {
                            highestIndecies.Clear();
                            highestIndecies.Add(a);
                            highestUcb = ucb;
                        }

                        else if (ucb == highestUcb)
                        {
                            highestIndecies.Add(a);
                        }
                    }
                    path.Add(path[path.Count - 1].nextBoards[highestIndecies[Random.Range(0, highestIndecies.Count)]]);
                }

                //expand the selected move
                Board leafNode = path[path.Count - 1];
                List<float> outComes = new List<float>();
                int simulations = 0;
                if (!leafNode.finished)
                {
                    leafNode.FindPossibleMoves();
                    for (int a = 0; a < leafNode.possibleMoves.Count; a++)
                        leafNode.nextBoards.Add(leafNode.MakeMove(leafNode.possibleMoves[a]));
                    for (int a = 0; a < leafNode.nextBoards.Count; a++)
                    {
                        Board nextBoard = leafNode.nextBoards[a];
                        nextBoard.Evaluate();
                        nextBoard.simulations++;
                        simulations++;
                        nextBoard.pastBoard = leafNode;
                        if (!nextBoard.p1 && Mathf.RoundToInt(nextBoard.eval) == 1)
                        {
                            //board from white's move results in white's win
                            nextBoard.wins++;
                            outComes.Add(1);
                        }

                        else if (nextBoard.p1 && Mathf.RoundToInt(nextBoard.eval) == -1)
                        {
                            //board from black's move results in black's win
                            nextBoard.wins++;
                            outComes.Add(-1);
                        }

                        else if (nextBoard.eval == 0)
                        {
                            //if game hasn't ended or move results in draw
                            outComes.Add(.5f);
                            nextBoard.wins += .5f;
                        }
                    }
                }

                else
                {
                    if (!leafNode.p1 && Mathf.RoundToInt(leafNode.eval) == 1)
                    {
                        //board from white's move results in white's win
                        leafNode.wins++;
                        outComes.Add(1);
                    }

                    else if (leafNode.p1 && Mathf.RoundToInt(leafNode.eval) == -1)
                    {
                        //board from black's move results in black's win
                        leafNode.wins += 1;
                        outComes.Add(-1);
                    }

                    else if (leafNode.eval == 0)
                    {
                        //move results in draw
                        leafNode.wins += .5f;
                        outComes.Add(.5f);
                    }
                    simulations++;
                }

                //backprop evaultion to current board
                for (int a = 0; a < path.Count; a++)
                {
                    for (int b = 0; b < outComes.Count; b++)
                    {
                        if (!path[a].p1 && outComes[b] == 1)
                            path[a].wins++;

                        else if (path[a].p1 && outComes[b] == -1)
                            path[a].wins++;

                        else if (outComes[b] == .5f)
                            path[a].wins += .5f;
                    }
                    path[a].simulations += simulations;
                }
            }

            moveSimulations.Clear();
            for(int a = 0; a < simulatedBoard.nextBoards.Count; a++)
            {
                moveSimulations.Add(simulatedBoard.nextBoards[a].simulations);
            }
            moveWins.Clear();
            for (int a = 0; a < simulatedBoard.nextBoards.Count; a++)
            {
                moveWins.Add(simulatedBoard.nextBoards[a].wins);
            }
        }
    }

    void ResetBoard()
    {
        timer += Time.deltaTime;
        if(timer > 3)
        {
            timer = 0;
            if (currentBoard.eval == 1)
                print("white won");

            else if (currentBoard.eval == -1)
                print("black won");

            else
                print("draw");

            currentBoard = new Board(this);
            currentBoard.FindPossibleMoves();
            game.SpawnPieces(currentBoard);
        }
    }
}
