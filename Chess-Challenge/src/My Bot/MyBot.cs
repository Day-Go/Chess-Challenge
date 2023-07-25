using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;




public struct CandidateMove
{
    public Move move;
    public double myScore;
    public double enemyScore;
}

public class MyBot : IChessBot
{
    Utils _utils;
    bool botIsWhite;
    EvalMetrics _evalMetrics;

    public MyBot()
    {
        _utils = new Utils();
        _evalMetrics = new EvalMetrics();
    }

    public Move Think(Board board, Timer timer)
    {
        botIsWhite = board.IsWhiteToMove;


        Move[] moves = board.GetLegalMoves();

        Move bestMove = moves[0];
        double bestScore = double.NegativeInfinity;
        foreach (Move move in OrderMoves(moves))
        {
            board.MakeMove(move);
            double score = -Minimax(board, 3, double.NegativeInfinity, double.PositiveInfinity);
            board.UndoMove(move);

            if (score > bestScore)
            {
                bestMove = move;
                bestScore = score;
            }
        }

        _evalMetrics.ClearCache();
        GC.Collect();

        return bestMove;
    }


    List<Move> OrderMoves(Move[] moves)
    {
        List<Move> orderedMoves = new List<Move>();
        foreach (Move move in moves)
        {
            if (move.IsCapture || move.IsPromotion)
            {
                orderedMoves.Insert(0, move);
            }
            else
            {
                orderedMoves.Add(move);
            }
        }
        return orderedMoves;
    }


    private double Minimax(Board board, int depth, double alpha, double beta)
    {
        if (depth == 0)
        {
            if (_evalMetrics.Cache.ContainsKey(board.ZobristKey))
            {
                return _evalMetrics.Cache[board.ZobristKey];
            }
            else
            {
                return ExecuteEvalLoop(board);
            }
        }

        Move[] moves = board.GetLegalMoves();
        double score = double.NegativeInfinity;

        foreach (Move move in OrderMoves(moves))
        {
            board.MakeMove(move);
            score = Math.Max(score, -Minimax(board, depth - 1, -beta, -alpha));
            board.UndoMove(move);

            alpha = Math.Max(alpha, score);
            if (alpha >= beta)
            {
                break; 
            }
        }

        return score;
    }

    private double ExecuteEvalLoop(Board board)
    {
        double score = 0;

        score += (board.IsWhiteToMove == botIsWhite ? 1 : -1) * _evalMetrics.ControlCenter(board);
        score += (board.IsWhiteToMove == botIsWhite ? 1 : -1) * _evalMetrics.DevelopPieces(board);
        score += (board.IsWhiteToMove == botIsWhite ? 1 : -1) * _evalMetrics.TotalMaterial(board);

        return score;
    }
}

// A set of heuristics
public class EvalMetrics
{
    Func<Board, ulong> _getBitBoard = b => b.IsWhiteToMove ? b.WhitePiecesBitboard : b.BlackPiecesBitboard;
    public Dictionary<ulong, double> Cache = new Dictionary<ulong, double>();
    int[] pieceValues = { 0, 100, 300, 300, 500, 900 };

    public void ClearCache()
    {
        Cache = new Dictionary<ulong, double>();
    }

    public double ControlCenter(Board board)
    {
        Cache[board.ZobristKey] = 0;

        ulong bitBoard = _getBitBoard(board);

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

        double result = MembershipFunctions.Sigmoidal(score, 1, 10);
        Cache[board.ZobristKey] += result;

        return result;
    }

    public double DevelopPieces(Board board)
    {
        ulong bitBoard = _getBitBoard(board);

        // 8th rank, 1st rank
        List<ulong> regions = new() { 18374686479671623680, 255 };


        double score = 0;
        foreach (ulong region in regions)
        {
            ulong overlap = region & bitBoard;

            score += CountSetBits(overlap);
        }

        double result = MembershipFunctions.Sigmoidal(score, 1, 10);
        Cache[board.ZobristKey] += result;

        return result;
    }


    public double ComparePins(Board board)
    {
        return 0;
    }

    public double TotalMaterial(Board board)
    {
        ulong bitBoard = _getBitBoard(board);

        double score = 0;

        for (int i = 0; i < 64; i++)
        {
            if (((bitBoard >> i) & 1) != 0)
            {
                var piece = board.GetPiece(new Square(i));

                if (!piece.IsKing)
                {
                    score += pieceValues[(int)piece.PieceType];
                }
            }
        }

        double result = MembershipFunctions.Sigmoidal(score, 0.002, 1950) * 10;
        Cache[board.ZobristKey] += result;

        return result;
    }


    public double DefendHangingPiece(Board board)
    {
        return 0;
    }

    // Kernighan's bit counting algorithm
    private int CountSetBits(ulong n)
    {
        int count = 0;
        while (n != 0)
        {
            n &= (n - 1);
            count++;
        }
        return count;
    }

}

public class MembershipFunctions
{
    public static double FastExp(double val)
    {
        long tmp = (long)(1512775 * val + 1072632447);
        return BitConverter.Int64BitsToDouble(tmp << 32);
    }


    public static double Sigmoidal(double x, double a, double c) =>
    1.0 / (1.0 + FastExp(-a * (x - c)));


    //public static double Sigmoidal(double x, double a, double c) =>
    //    1.0 / (1.0 + Math.Exp(-a * (x - c)));
}


public class Utils
{
    public string BitBoardToString(ulong pieceBitBoard)
    {
        return Convert.ToString((long)pieceBitBoard, 2).PadLeft(64, '0');
    }
}