using FreeAgencyAuctionAPI.Models.Confidence;
using System.Collections.Generic;
using System.Linq;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IConfidencePickValidationService
    {
        ValidationResult ValidatePickSubmission(NflPickSubmission submission, int expectedMatchupCount, int expectedPropCount);
    }

    public class ConfidencePickValidationService : IConfidencePickValidationService
    {
        public ValidationResult ValidatePickSubmission(NflPickSubmission submission, int expectedMatchupCount, int expectedPropCount)
        {
            if (submission == null)
            {
                return ValidationResult.Failure("Pick submission cannot be null.");
            }

            var picks = submission.Picks ?? new List<NflPicksDTO>();
            var props = submission.Props ?? new List<PropPickDTO>();

            // Validate picks exist
            if (!picks.Any())
            {
                return ValidationResult.Failure("At least one pick is required.");
            }

            // Validate all picks have same owner
            var ownerIds = picks.Select(p => p.OwnerId).Distinct().ToList();
            if (ownerIds.Count != 1 || ownerIds[0] == 0)
            {
                return ValidationResult.Failure("All picks must belong to the same valid owner.");
            }

            var ownerId = ownerIds[0];

            // Validate props have same owner (if props exist)
            if (props.Any())
            {
                var propOwnerIds = props.Select(p => p.OwnerId).Distinct().ToList();
                if (propOwnerIds.Count != 1 || propOwnerIds[0] != ownerId)
                {
                    return ValidationResult.Failure("All props must belong to the same owner as picks.");
                }
            }

            // Validate correct number of picks
            if (picks.Count != expectedMatchupCount)
            {
                return ValidationResult.Failure($"Expected {expectedMatchupCount} picks but received {picks.Count}.");
            }

            // Validate no duplicate matchups
            var matchupIds = picks.Select(p => p.MatchupId).ToList();
            if (matchupIds.Distinct().Count() != matchupIds.Count)
            {
                return ValidationResult.Failure("Duplicate matchup picks detected.");
            }

            // Validate points are sequential (no gaps, no duplicates)
            var points = picks.Select(p => p.Points).OrderBy(p => p).ToList();
            
            // Check for duplicates
            if (points.Distinct().Count() != points.Count)
            {
                return ValidationResult.Failure("Duplicate point values detected. Each pick must have a unique point value.");
            }

            // Check that points form a continuous sequence (no gaps)
            // For example: [2,3,4,5] is valid, [1,2,4,5] is invalid (gap at 3)
            var minPoint = points.First();
            var maxPoint = points.Last();
            var expectedRange = Enumerable.Range(minPoint, expectedMatchupCount).ToList();
            
            if (!points.SequenceEqual(expectedRange))
            {
                return ValidationResult.Failure(
                    $"Points must be sequential with no gaps. Expected range from {minPoint} to {maxPoint} but got: {string.Join(", ", points)}.");
            }

            // Validate all picks have a choice
            if (picks.Any(p => !p.Choice.HasValue))
            {
                return ValidationResult.Failure("All picks must have a team choice.");
            }

            // Validate props if expected
            if (expectedPropCount > 0)
            {
                if (props.Count != expectedPropCount)
                {
                    return ValidationResult.Failure($"Expected {expectedPropCount} prop picks but received {props.Count}.");
                }

                // Validate no duplicate props
                var propIds = props.Select(p => p.PropId).ToList();
                if (propIds.Distinct().Count() != propIds.Count)
                {
                    return ValidationResult.Failure("Duplicate prop picks detected.");
                }

                // Validate all props have a choice (A or B)
                if (props.Any(p => string.IsNullOrEmpty(p.Choice) || (p.Choice != "A" && p.Choice != "B")))
                {
                    return ValidationResult.Failure("All prop picks must have a choice of 'A' or 'B'.");
                }
            }

            return ValidationResult.Success(ownerId);
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; }
        public int OwnerId { get; private set; }

        private ValidationResult() { }

        public static ValidationResult Success(int ownerId)
        {
            return new ValidationResult
            {
                IsValid = true,
                OwnerId = ownerId
            };
        }

        public static ValidationResult Failure(string errorMessage)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
