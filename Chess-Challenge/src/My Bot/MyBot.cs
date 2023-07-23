using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public struct CandidateMove
{
    public Move move;
    public double myScore;
    public double enemyScore;
}

public class MyBot : IChessBot
{
    Utils _utils;
    MethodInfo[] _methods;

    public MyBot()
    {
        _methods = typeof(EvalMetrics).GetMethods();
        _utils = new Utils();
    }

    public Move Think(Board board, Timer timer)
    {
        ulong myPieceBitboard;
        ulong enemyPieceBitBoard;

        List<CandidateMove> candidateMoves = new ();

        // Evaulate moves
        Move[] moves = board.GetLegalMoves();

        Move nextMove = moves[0];

        foreach (Move move in moves)
        {
            board.MakeMove(move);

            myPieceBitboard = GetMyBitBoard(board);
            enemyPieceBitBoard = GetEnemyBitBoard(board, myPieceBitboard);

            double myScore = ExecuteEvalLoop(myPieceBitboard);
            double enemyScore = ExecuteEvalLoop(enemyPieceBitBoard);

            AddCandidateMove(candidateMoves, new CandidateMove { 
                move = move, myScore = myScore, enemyScore = enemyScore });

            board.UndoMove(move);
        }

        double maxScore = -100;
        foreach (CandidateMove candidateMove in candidateMoves)
        {
            // Make move so enemy has updated board
            board.MakeMove(candidateMove.move);

            Move[] enemyMoves = board.GetLegalMoves();
            foreach (Move move in enemyMoves)
            {
                // Make move so enemy has updated board
                board.MakeMove(move);

                myPieceBitboard = GetMyBitBoard(board);
                enemyPieceBitBoard = GetEnemyBitBoard(board, myPieceBitboard);

                double myScore = ExecuteEvalLoop(myPieceBitboard);
                double enemyScore = ExecuteEvalLoop(enemyPieceBitBoard);

                if (myScore - enemyScore > maxScore)
                {
                    nextMove = candidateMove.move;
                    maxScore = myScore - enemyScore;
                }


                board.UndoMove(move);
            }


            board.UndoMove(candidateMove.move);
        }


        return nextMove;
    }


    private double ExecuteEvalLoop(ulong bitBoard)
    {
        double score = 0;
        // Apply all rules
        foreach (MethodInfo method in _methods)
        {
            // Check if the method has any parameters, since your methods are parameterless.
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ulong))
            {
                // If method requires ulong argument, invoke with the required argument
                score += (double) method.Invoke(null, new object[] { bitBoard });
            }
        }

        return score;
    }

    private ulong GetMyBitBoard(Board board)
    {
        return board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
    }

    private ulong GetEnemyBitBoard(Board board, ulong myBitBoard)
    {
        return board.AllPiecesBitboard ^ myBitBoard;
    }

    public void AddCandidateMove(List<CandidateMove> candidateMoves, CandidateMove newMove)
    {
        // If the list is not full, just add the new move
        if (candidateMoves.Count < 5)
        {
            candidateMoves.Add(newMove);
            return;
        }

        // Find the move with the worst score
        double minScore = candidateMoves.Min(move => move.myScore);
        CandidateMove worstMove = candidateMoves.First(move => move.myScore == minScore);

        // If the new move has a better score, replace the worst move
        if (newMove.myScore > worstMove.myScore)
        {
            candidateMoves.Remove(worstMove);
            candidateMoves.Add(newMove);
        }
    }

    private void Search(Board board, int depth) 
    {
        
    }

    private ulong SimulateMove(Move move, ref ulong bitBoard)
    {
        BitboardHelper.SetSquare(ref bitBoard, move.TargetSquare);
        BitboardHelper.ClearSquare(ref bitBoard, move.StartSquare);

        return bitBoard;
    }

    private ulong UndoSimulatedMove(Move move, ref ulong bitBoard)
    {
        BitboardHelper.SetSquare(ref bitBoard, move.StartSquare);
        BitboardHelper.ClearSquare(ref bitBoard, move.TargetSquare);

        return bitBoard;
    }
}

// A set of heuristics
public class EvalMetrics
{
    public static double ControlCenter(ulong bitBoard)
    {
        List<ulong> regions = new() { 35538699412471296, 66125924401152, 103481868288 };

        int score = 0, multiplier = 1;

        foreach (ulong region in regions)
        {
            ulong overlap = region & bitBoard;

            // Count number of ones in binary representation
            while (overlap > 0)
            {
                // Increase count if the least significant bit is 1
                score += (int)overlap & 1 * multiplier;

                // Right shift the bits of overlap
                overlap >>= 1;
            }

            multiplier += 2;
        }

        return MembershipFunctions.Sigmoidal(score, 1, 10);
    }

    public static double DevelopPieces(ulong bitBoard)
    {
        return 0;
    }

    public static double ComparePins(ulong bitBoard)
    {
        return 0;
    }

    public static double MaterialAdvantage(ulong bitBoard)
    {
        return 0;
    }

    public static double DefendHangingPiece(ulong bitBoard)
    {
        return 0;
    }
}


// the methods in this class will be passed a single value from the result of a rule
// depending on game state we will use different membership functions of different rules. 
public class MembershipFunctions
{
    public static double Sigmoidal(double x, double a, double c) =>
        1.0 / (1.0 + Math.Exp(-a * (x - c)));
}


public class Utils
{
    public string BitBoardToString(ulong pieceBitBoard)
    {
        return Convert.ToString((long)pieceBitBoard, 2).PadLeft(64, '0');
    }
}