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

        // Get a list of all legal moves.
        Move[] moves = board.GetLegalMoves();

        // Initialize the best move and the best score.
        Move bestMove = moves[0];
        double bestScore = double.NegativeInfinity;

        // Perform the minimax search with alpha-beta pruning.
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            double score = -Minimax(board, 3, double.NegativeInfinity, double.PositiveInfinity);
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
    Dictionary<ulong, double> controlCache = new Dictionary<ulong, double>();
    Dictionary<ulong, double> developCache = new Dictionary<ulong, double>();
    Dictionary<ulong, double> materialCache = new Dictionary<ulong, double>();
    int[] pieceValues = { 0, 100, 300, 300, 500, 900 };

    public double ControlCenter(Board board)
    {
        ulong bitBoard = _getBitBoard(board);

        if (controlCache.ContainsKey(bitBoard)) return controlCache[bitBoard];
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
        controlCache[bitBoard] = result;

        return result;
    }

    public double DevelopPieces(Board board)
    {
        ulong bitBoard = _getBitBoard(board);

        if (developCache.ContainsKey(bitBoard)) return developCache[bitBoard];

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

        double result = MembershipFunctions.Sigmoidal(score, 1, 10);
        developCache[bitBoard] = result;

        return result;
    }
 

    public double ComparePins(Board board)
    {
        return 0;
    }

    public double TotalMaterial(Board board)
    {
        ulong bitBoard = _getBitBoard(board);

        if (materialCache.ContainsKey(bitBoard)) return materialCache[bitBoard];

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
        materialCache[bitBoard] = result;

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
