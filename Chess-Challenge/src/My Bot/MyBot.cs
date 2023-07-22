using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;

public class MyBot : IChessBot
{

    MethodInfo[] _methods;

    public MyBot()
    {
        _methods = typeof(FuzzyRules).GetMethods();
    }

    public Move Think(Board board, Timer timer)
    {
        double minFitness = 100;
        double fitness;

        Move[] moves = board.GetLegalMoves();
        Move nextMove = moves[0];

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            fitness = ExecuteEvaluationLoop(board, move, minFitness, false);

            Move[] enemyMoves = board.GetLegalMoves();

            foreach (Move enemyMove in enemyMoves)
            {
                board.MakeMove(enemyMove);
                fitness = ExecuteEvaluationLoop(board, enemyMove, minFitness, true);

                if (fitness < minFitness)
                {
                    minFitness = fitness;
                    nextMove = move;
                }

                board.UndoMove(enemyMove);
            }
            board.UndoMove(move);


        }

        return nextMove;
    }

    private double ExecuteEvaluationLoop(Board board, Move move, double maxFitness, bool flipMove)
    {

        // Instantiate rules on each move.
        // More cpu intensive but less tokens. 
        FuzzyRules rules = new(board, flipMove);

        double fitness = 0;
        // Apply all rules
        foreach (MethodInfo method in _methods)
        {
            // Check if the method has any parameters, since your methods are parameterless.
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0 && method.ReturnType == typeof(double))
            {
                // If it's a static method, the first argument is null.
                // Otherwise, pass in the instance of the class (fuzzyRulesInstance).
                fitness += (double)method.Invoke(rules, null);
            }
        }

        Console.WriteLine($"{move.StartSquare} -> {move.TargetSquare}");
        Console.WriteLine($"{fitness}");
        Console.WriteLine($"{maxFitness}");
        Console.WriteLine($"");

        return fitness;
    }
}

public class TacticAnalyser
{

}

// A set of heuristics
public class FuzzyRules
{
    Utils _utils;
    Board _board;

    bool isWhite;
    ulong myPiecesBitBoard;
    ulong enemyPiecesBitBoard;

    string myPiecesBitBoardString;
    string enemyPiecesBitBoardString;

    List<List<int>> myPiecesBitBoardArray;
    List<List<int>> enemyPiecesBitBoardArray;


    public FuzzyRules(Board board, bool flipMove)
    {
        _utils = new Utils();
        _board = board;

        // Pre-assign commonly used values to save tokens
        isWhite = flipMove ? !board.IsWhiteToMove : board.IsWhiteToMove;
        myPiecesBitBoard = isWhite ? board.BlackPiecesBitboard : board.WhitePiecesBitboard;
        enemyPiecesBitBoard = isWhite ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;

        // Preprocessing for representations that are used across multiple methods.
        myPiecesBitBoardString = _utils.BitBoardToString(myPiecesBitBoard);
        enemyPiecesBitBoardString = _utils.BitBoardToString(enemyPiecesBitBoard);

        myPiecesBitBoardArray = _utils.BitBoardStringToIntArray(myPiecesBitBoardString);
        enemyPiecesBitBoardArray = _utils.BitBoardStringToIntArray(enemyPiecesBitBoardString);
    }

    // Implement a set of basic rules
    public double ControlCenter()
    {
        Func<int, int> calculateDistance = x => x < 4 ? x : 7 - x;

        var boardCenterMask = Enumerable.Range(0, 8)
                                        .Select(i => Enumerable.Range(0, 8)
                                                               .Select(j => calculateDistance(i) + calculateDistance(j))
                                                               .ToList())
                                        .ToList();

        double myCenterControl = 0;
        double enemyCenterControl = 0;

        for (int r = 0; r < 8; r++)
        {
            for (int f = 0; f < 8; f++)
            {
                myCenterControl += myPiecesBitBoardArray[r][f] * boardCenterMask[r][f];
                enemyCenterControl += enemyPiecesBitBoardArray[r][f] * boardCenterMask[r][f];
            }
        }

        Console.WriteLine(myCenterControl);
        Console.WriteLine(enemyCenterControl);

        var res = MembershipFunctions.Sigmoidal(myCenterControl / enemyCenterControl, 0.5, 4);
        Console.WriteLine($"con - {res}");
        return MembershipFunctions.Sigmoidal(myCenterControl / enemyCenterControl, 0.5, 4);
    }

    public double DevelopPieces()
    {
        double myDevelopment = isWhite ? myPiecesBitBoardArray[0].Count(i => i == 1) :
                                         myPiecesBitBoardArray[7].Count(i => i == 1);

        double enemyDevelopment = !isWhite ? enemyPiecesBitBoardArray[0].Count(i => i == 1) :
                                             enemyPiecesBitBoardArray[7].Count(i => i == 1);

        var res = MembershipFunctions.Sigmoidal(enemyDevelopment / myDevelopment, 0.5, 4);
        Console.WriteLine($"dev - {res}");
        return MembershipFunctions.Sigmoidal(enemyDevelopment / myDevelopment, 0.5, 4);
    }

    public double ComparePins()
    {
        return 0;
    }

    public double MaterialAdvantage()
    {
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
        double[] materials = { 0, 0 };

        for (int r = 1; r < 9; r++)
        {
            for (int f = 0; f < 8; f++)
            {
                var piece = _board.GetPiece(new Square($"{(char)('a' + f)}{r}"));
                materials[piece.IsWhite == isWhite ? 0 : 1] += pieceValues[(int)piece.PieceType];
            }
        }

        var res = MembershipFunctions.Sigmoidal(materials[0] / materials[1], 0.5, 1.5);
        Console.WriteLine($"mat - {res}");
        return MembershipFunctions.Sigmoidal(materials[0] / materials[1], 0.5, 1.5);
    }

    public double DefendHangingPiece()
    {
        // Count hanging pieces and value
        return 0;
    }
}

public enum PieceValues
{

}

// the methods in this class will be passed a single value from the result of a rule
// depending on game state we will use different membership functions of different rules. 
public class MembershipFunctions
{
    public static double Triangular(double x, double a, double b, double c) =>
        x <= a || x >= c ? 0.0 : x < b ? (x - a) / (b - a) : (c - x) / (c - b);

    public static double Trapezoidal(double x, double a, double b, double c, double d) =>
        x <= a || x >= d ? 0.0 : x < b ? (x - a) / (b - a) : x <= c ? 1.0 : (d - x) / (d - c);

    public static double Gaussian(double x, double mean, double standardDeviation) =>
        Math.Exp(-0.5 * Math.Pow((x - mean) / standardDeviation, 2));

    public static double Sigmoidal(double x, double a, double c) =>
        1.0 / (1.0 + Math.Exp(-a * (x - c)));
}


public class Utils
{
    //public ulong FlipBitBoard(ulong b) => 
    //    Enumerable.Range(0, 64).Aggregate(0UL, (r, i) => 
    //    r | (((b >> i) & 1) << (63 - i)));

    // original implementation
    public ulong FlipBitBoard(ulong pieceBitBoard)
    {
        ulong reversed = 0;
        for (int i = 0; i < 64; i++)
        {
            ulong bit = (pieceBitBoard >> i) & 1;
            reversed |= bit << (63 - i);
        }
        return reversed;
    }

    public string BitBoardToString(ulong pieceBitBoard)
    {
        return Convert.ToString((long)pieceBitBoard, 2).PadLeft(64, '0');
    }

    public List<List<int>> BitBoardStringToIntArray(string bitBoard)
    {
        int size = 8;  // Size of one dimension

        return Enumerable.Range(0, size)
            .Select(i => Enumerable.Range(0, size)
                .Select(j => int.Parse(bitBoard[j + i * size].ToString()))
                .ToList())
            .ToList();
    }


}