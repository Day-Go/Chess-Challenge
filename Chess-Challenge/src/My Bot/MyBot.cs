using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel;

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
    bool botIsWhite;

    public MyBot()
    {
        _methods = typeof(EvalMetrics).GetMethods();
        _utils = new Utils();
    }

    public Move Think(Board board, Timer timer)
    {
        botIsWhite = board.IsWhiteToMove;

        // Get a list of all legal moves.
        Move[] moves = board.GetLegalMoves();

        // Initialize the best move and the best score.
        Move bestMove = moves[0];
        double bestScore = double.NegativeInfinity;

        // Perform the minimax search with alpha-beta pruning.
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            double score = -Minimax(board, 5, double.NegativeInfinity, double.PositiveInfinity);
            board.UndoMove(move);

            // Update the best move and the best score.
            if (score > bestScore)
            {
                bestMove = move;
                bestScore = score;
            }
        }

        

        // Return the best move.
        return bestMove;
    }

    private double Minimax(Board board, int depth, double alpha, double beta)
    {
        if (depth == 0)
        {
            return ExecuteEvalLoop(board);
        }

        Move[] moves = board.GetLegalMoves();
        double score = double.NegativeInfinity;

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            score = Math.Max(score, -Minimax(board, depth - 1, -beta, -alpha));
            board.UndoMove(move);

            alpha = Math.Max(alpha, score);
            if (alpha >= beta)
            {
                break;  // Alpha-beta pruning
            }
        }

        return score;
    }

    private double ExecuteEvalLoop(Board board)
    {
        EvalMetrics evalMetrics = new(board);

        double score = 0;
        foreach (MethodInfo method in _methods)
        {
            // Check if the method has any parameters, since your methods are parameterless.
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0 && method.ReturnType == typeof(double))
            {
                // If method requires ulong argument, invoke with the required argument
                if (board.IsWhiteToMove == botIsWhite)
                {
                    score += (double)method.Invoke(evalMetrics, null);
                }
                else
                {
                    score -= (double)method.Invoke(evalMetrics, null);
                }
            }
        }

        return score;
    }
}

// A set of heuristics
public class EvalMetrics
{
    Board _board;
    Func<Board, ulong> _getBitBoard = b => b.IsWhiteToMove ? b.WhitePiecesBitboard : b.BlackPiecesBitboard;
    ulong bitBoard;


    public EvalMetrics(Board board)
    {
        _board = board;
        bitBoard = _getBitBoard(_board);
    }

    public double ControlCenter()
    {

        // Outer circle, Inner circle, Center
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

    public double DevelopPieces()
    {
        // 8th rank, 1st rank
        List<ulong> regions = new() { 18374686479671623680, 255 };

        double score = 0;
        foreach (ulong region in regions)
        {
            ulong overlap = region & bitBoard;

            // Count number of ones in binary representation
            while (overlap > 0)
            {
                // Increase count if the least significant bit is 1
                score += (int)overlap & 1;

                // Right shift the bits of overlap
                overlap >>= 1;
            }
        }

        score = 8 - score;

        return MembershipFunctions.Sigmoidal(score, 1, 10);
    }


    public double ComparePins()
    {
        return 0;
    }

    public double MaterialAdvantage()
    {
        int[] pieceValues = { 0, 100, 300, 300, 500, 900 };

        double score = 0;

        for (int i = 0; i < 64; i++)
        {
            if (((bitBoard >> i) & 1) != 0)
            {
                var piece = _board.GetPiece(new Square(i));

                if (!piece.IsKing)
                {
                    score += pieceValues[(int)piece.PieceType];
                }
            }
        }

        return MembershipFunctions.Sigmoidal(score, 0.002, 3950) * 3;
    }


    public double DefendHangingPiece()
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