// Verifica TurnsCannotReverseOrChainBeforeTheNextMoveAndWallsEndTheRound, EatingGrowsTheSnakeAndNewFoodAlwaysUsesAFreeCell, AFullBoardWinsWithoutTryingToSpawnFoodForever, TurningIntoTheBodyEndsTheRound.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HorrorGame.Desktop;

namespace HorrorGame.Tests
{
    [Category("PCGamesData")]
    public sealed class DesktopSnakeTests
    {
        [Test]
        public void TurnsCannotReverseOrChainBeforeTheNextMoveAndWallsEndTheRound()
        {
            var board=new SnakeBoard(7);
            var head=board.Segments[0];
            Assert.That(board.Turn(Vector2Int.left),Is.False);
            Assert.That(board.Turn(new Vector2Int(1,1)),Is.False);
            Assert.That(board.Turn(Vector2Int.up),Is.True);
            Assert.That(board.Turn(Vector2Int.left),Is.False,"Two turns in one tick cannot reverse into the neck.");
            Assert.That(board.Step(),Is.True);Assert.That(board.Segments[0],Is.EqualTo(head+Vector2Int.up));
            while(!board.HasLost)board.Step();
            Assert.That(board.Step(),Is.False);Assert.That(board.HasWon,Is.False);
        }

        [Test]
        public void EatingGrowsTheSnakeAndNewFoodAlwaysUsesAFreeCell()
        {
            var board=Enumerable.Range(0,1000).Select(seed=>new SnakeBoard(seed))
                .First(b=>b.Food.y==b.Segments[0].y&&b.Food.x>b.Segments[0].x);
            var food=board.Food;
            while(board.Segments[0]!=food)Assert.That(board.Step(),Is.True);
            Assert.That(board.Score,Is.EqualTo(1));Assert.That(board.Segments.Count,Is.EqualTo(4));
            Assert.That(board.Segments.Contains(board.Food),Is.False);
        }

        static List<Vector2Int> Cycle()
        {
            var cells=new List<Vector2Int>();
            for(int x=0;x<6;x++)cells.Add(new Vector2Int(x,0));
            for(int y=1;y<4;y++)
                if((y&1)==1)for(int x=5;x>=1;x--)cells.Add(new Vector2Int(x,y));
                else for(int x=1;x<6;x++)cells.Add(new Vector2Int(x,y));
            for(int y=3;y>=1;y--)cells.Add(new Vector2Int(0,y));
            return cells;
        }

        [Test]
        public void AFullBoardWinsWithoutTryingToSpawnFoodForever()
        {
            var board=new SnakeBoard(17,6,4);var cycle=Cycle();int moves=0;
            while(!board.HasWon&&moves++<600)
            {
                int index=cycle.IndexOf(board.Segments[0]);
                board.Turn(cycle[(index+1)%cycle.Count]-board.Segments[0]);
                Assert.That(board.Step(),Is.True);
            }
            Assert.That(board.HasWon,Is.True);Assert.That(board.Score,Is.EqualTo(21));
            Assert.That(board.Step(),Is.False);Assert.That(board.Turn(Vector2Int.up),Is.False);
        }

        [Test]
        public void TurningIntoTheBodyEndsTheRound()
        {
            var board=new SnakeBoard(41,6,4);var cycle=Cycle();
            for(int moves=0;moves<600&&!board.HasWon;moves++)
            {
                foreach(var turn in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                {
                    var next=board.Segments[0]+turn;int index=board.Segments.ToList().IndexOf(next);
                    if(index>0&&index<board.Segments.Count-1&&turn!=-board.Direction)
                    {Assert.That(board.Turn(turn),Is.True);Assert.That(board.Step(),Is.False);Assert.That(board.HasLost,Is.True);return;}
                }
                int at=cycle.IndexOf(board.Segments[0]);board.Turn(cycle[(at+1)%cycle.Count]-board.Segments[0]);
                Assert.That(board.Step(),Is.True);
            }
            Assert.Fail("Expected a body-collision opportunity while filling the board.");
        }
    }
}
