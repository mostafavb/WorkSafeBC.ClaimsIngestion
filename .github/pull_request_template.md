## Summary

<!-- What changed and why? -->

## Change Type

- [ ] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Infrastructure / deployment
- [ ] Test-only
- [ ] Documentation

## Architecture Impact

- [ ] Domain
- [ ] Application
- [ ] Infrastructure
- [ ] Worker / orchestration
- [ ] Bicep / deployment
- [ ] No architecture boundary changes

## Validation

- [ ] `dotnet build WorkSafeBC.ClaimsIngestion.slnx -c Release`
- [ ] Unit tests
- [ ] Architecture tests
- [ ] Contract tests
- [ ] Integration tests
- [ ] Not applicable

## Event and Data Contract Checklist

- [ ] Published event shape is unchanged
- [ ] Contract tests were updated for event changes
- [ ] Blob/file parsing behavior is unchanged
- [ ] Sample payloads were validated when parsing or messaging changed

## Security and Configuration

- [ ] No secrets were committed
- [ ] GitHub Secrets / Key Vault / environment settings were updated if needed
- [ ] Bicep or workflow changes were reviewed for least-privilege impact
- [ ] Not applicable

## Observability

- [ ] Existing telemetry still covers success, failure, and latency
- [ ] New flow or failure paths emit useful logs/metrics/traces
- [ ] Not applicable

## Rollback Notes

<!-- How would this be rolled back if it causes issues? -->
