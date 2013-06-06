﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using NUnit.Framework;

namespace NTestCaseBuilder.Examples
{
    ///<summary>
    ///  Test fixture for class 'SortingAlgorithmModule'.
    ///</summary>
    [TestFixture]
    public class TestSortingAlgorithm
    {
        ///<summary>
        ///  A test case to apply sorting to. Provides a sequence of integers in some unspecified order
        ///  - may or may not be sorted in ascending order. Some of the integers in the sequence may be
        ///  duplicated; the duplicates may or may not be adjacent to each other.
        ///  The sequence is generated by permuting a sequence of integers that is known by construction
        ///  to be monotonic increasing, with any duplicates arranged into runs of adjacent duplicated
        ///  values. This base sequence is also made available to check the expected results from any sorting
        ///  algorithm.
        ///</summary>
        public class TestCase
        {
            ///<summary>
            ///  Constructor for use by synthesizing factory.
            ///</summary>
            ///<param name = "leastItemInSequence">The lowest value that starts off <cref>OriginalMonotonicIncreasingSequence</cref></param>
            ///<param name = "nonNegativeDeltas">Sequence of non-negative deltas that will be used to build up <cref>OriginalMonotonicIncreasingSequence</cref></param>
            ///<param name = "permutation">A permutation that is used to shuffle <cref>OriginalMonotonicIncreasingSequence</cref> to give <cref>PermutedSequence</cref></param>
            public TestCase(Int32 leastItemInSequence, IEnumerable<UInt32> nonNegativeDeltas,
                            Permutation<Int32> permutation)
            {
                var originalMonotonicIncreasingSequence = new List<Int32>();

                var runningSum = leastItemInSequence;

                foreach (var nonNegativeDelta in nonNegativeDeltas)
                {
                    originalMonotonicIncreasingSequence.Add(runningSum);
                    runningSum += (Int32) nonNegativeDelta;
                }

                originalMonotonicIncreasingSequence.Add(runningSum);

                OriginalMonotonicIncreasingSequence = originalMonotonicIncreasingSequence;

                PermutedSequence = permutation(originalMonotonicIncreasingSequence);
            }

            ///<summary>
            ///  Parameterless constructor that represents the trivial empty sequence case.
            ///</summary>
            public TestCase()
            {
                OriginalMonotonicIncreasingSequence = new List<Int32>();

                PermutedSequence = new List<Int32>();
            }

            /// <summary>
            ///   The sequence to be used as input to a sorting algorithm.
            /// </summary>
            public IEnumerable<Int32> PermutedSequence { get; set; }

            ///<summary>
            ///  The expected result of sorting <cref>PermutedSequence</cref>.
            ///</summary>
            public IEnumerable<Int32> OriginalMonotonicIncreasingSequence { get; set; }
        }

        private static TypedTestCaseEnumerableFactory<TestCase> BuildTestCaseFactory()
        {
            var factoryForLeastItemInSequence = TestVariableLevelEnumerableFactory.Create(Enumerable.Range(-3, 10));

            const int maximumNumberOfDeltas = 5;

            var factoryForNonNegativeDeltasAndPermutation =
                InterleavedTestCaseEnumerableFactory.Create(
                    from numberOfDeltas in Enumerable.Range(0, 1 + maximumNumberOfDeltas)
                    select BuildNonNegativeDeltasAndPermutationFactory(numberOfDeltas));

            var testCaseFactoryForTrivialCase = SingletonTestCaseEnumerableFactory.Create(new TestCase());

            var testCaseFactoryForNonTrivialCases =
                SynthesizedTestCaseEnumerableFactory.Create(factoryForLeastItemInSequence,
                                                            factoryForNonNegativeDeltasAndPermutation,
                                                            (leastItemInSequence, nonNegativeDeltasAndItsPermutation) =>
                                                            new TestCase(leastItemInSequence,
                                                                         nonNegativeDeltasAndItsPermutation.Item1,
                                                                         nonNegativeDeltasAndItsPermutation.Item2));

            return
                InterleavedTestCaseEnumerableFactory.Create(new[]
                                                                {
                                                                    testCaseFactoryForTrivialCase,
                                                                    testCaseFactoryForNonTrivialCases
                                                                });
        }

        private static TypedTestCaseEnumerableFactory<Tuple<FSharpList<UInt32>, Permutation<Int32>>>
            BuildNonNegativeDeltasAndPermutationFactory(int numberOfDeltas)
        {
            var factoryForNonNegativeDelta =
                TestVariableLevelEnumerableFactory.Create(from signedDelta in Enumerable.Range(0, 5)
                                                          select (UInt32) signedDelta);
            return
                SynthesizedTestCaseEnumerableFactory.CreateWithPermutation<UInt32, Int32>(
                    Enumerable.Repeat(factoryForNonNegativeDelta, numberOfDeltas));
        }

        ///<summary>
        ///  Parameterised unit test for <cref>SortingAlgorithmModule.SortWithBug</cref>.
        ///</summary>
        ///<remarks>
        ///  This is expected to fail.
        ///</remarks>
        ///<param name = "testCase"></param>
        public static void
            ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm(
            TestCase testCase)
        {
            Console.WriteLine("[{0}]", String.Join(", ", testCase.PermutedSequence));

            var sortedSequence = SortingAlgorithmModule.SortWithBug(testCase.PermutedSequence);

            Assert.IsTrue(sortedSequence.SequenceEqual(testCase.OriginalMonotonicIncreasingSequence));
        }

        ///<summary>
        ///  Parameterised unit test for <cref>SortingAlgorithmModule.SortThatWorks</cref>.
        ///</summary>
        ///<remarks>
        ///  This is expected to succeed.
        ///</remarks>
        ///<param name = "testCase"></param>
        public static void
            ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByCorrectSortingAlgorithm(
            TestCase testCase)
        {
            Console.WriteLine("[{0}]", String.Join(", ", testCase.PermutedSequence));

            var sortedSequence = SortingAlgorithmModule.SortThatWorks(testCase.PermutedSequence);

            Assert.IsTrue(sortedSequence.SequenceEqual(testCase.OriginalMonotonicIncreasingSequence));
        }

        ///<summary>
        ///  Unit test for <cref>SortingAlgorithmModule.SortWithBug</cref>.
        ///</summary>
        [Test]
        public void TestReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm()
        {
            var factory = BuildTestCaseFactory();
            const Int32 strength = 3;

            var howManyTestCasesWereExecuted = factory.ExecuteParameterisedUnitTestForAllTypedTestCases(strength,
                                                                                                        ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm);

            Console.WriteLine("Executed {0} test cases successfully.", howManyTestCasesWereExecuted);
        }

        ///<summary>
        ///  Unit test for <cref>SortingAlgorithmModule.SortWithBug</cref>.
        ///</summary>
        [Test]
        public void TestReassemblyOfPermutedMonotonicIncreasingSequenceByCorrectSortingAlgorithm()
        {
            var factory = BuildTestCaseFactory();
            const Int32 strength = 3;

            var howManyTestCasesWereExecuted = factory.ExecuteParameterisedUnitTestForAllTypedTestCases(strength,
                                                                                                        ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByCorrectSortingAlgorithm);

            Console.WriteLine("Executed {0} test cases successfully.", howManyTestCasesWereExecuted);
        }

        ///<summary>
        ///  Reproduce the test failure from <cref>TestReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm</cref>.
        ///</summary>
        [Test]
        public void TestThatQuicklyReproducesTheFailureFromTheBuggyTest()
        {
            const string reproduction =
                // This is cut and paste from the exception thrown by test TestReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm.
                "1090764779116129690923515858308014520222336185700694896976936400046940578111983112055989629000774433035533486068550533022050440563758532034744094390335385597493640149399285518641151929556092665584402288546355440347730368088836771627466259556412021922628617830401308197993420136306095381236390659859395474282333923663195001674855077610370661920394352760048210972093417030611663008489291390142329599624363289740294540713600820585626858909178813737905470453455593125809517419504379797718624042146020287243556149349408179275320435576035084233467266313678953281047913243828196624730764539193598305067642050167579717616487986610616926209267154939751727929885359578737455608152137192620364367008197439004774155768715213295028907456424168317197337955840788615403009918867556577991269184091946861394300712946499906809118488577336848236813433114641537205630599186179262409744388845590557296897842868035670598403665595421628569316025764437535305515574206477435978865716717384210158043714838771887683662361214061771150557721070949634422618397436611595817374189673643054451752162382772507512789254132581449654984193629343428332084395761618311646782155591779327216910309170447926754446372758888480333864013289445818385865334616144969362520105029142173149661293110970637187810980530406290600226803805773851569289479651729233784482850244149270293881505450855446583871745275937315186042456826787678065150273054033695378782748192993549658646923114748726127674648141970418942763956195530374463711950064469221388984826928307781666512772058179375177331936895709873684328138974090330169544220040297640808408784184007637825153641679593162138872521985525908382375673982891522776006721300646241282132286789404502921817467667921915664078495290242856821493406938477191945925059190392131486689610077498097776810232710037410131639483496653685678502067318242597067099220651158362316022086874353049914915824302554251813213220248313540345911378764039453350655160292339989697069103604137125320303640156630906043605438877368214578964196680895049115987289019715105714708318637178713665546323587972552593785366609231484096218938280792422416247653513276525750301510028101375578485838914560323108522219894193669902918961407329180871420283323143495470594139368741371143042432078241408297802802549695699445263815815073130282911745887420917959479553640232222986770300950738306765627369220497365295793222457972819026998719696411586631880338056327478751561832136060579356468492164599268339149893776068789141504";

            var factory = BuildTestCaseFactory();

            factory.ExecuteParameterisedUnitTestForReproducedTypedTestCase(
                ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm,
                reproduction);
        }
    }
}