# vim: noexpandtab:ts=4:sw=4
.PHONY: build test test-all

RABBITMQ_DOCKER_NAME ?= rabbitmq-dotnet-client-rabbitmq

build:
	dotnet build $(CURDIR)/Build.csproj

test:
	dotnet test $(CURDIR)/projects/Test/Unit/Unit.csproj --logger 'console;verbosity=detailed'
	dotnet test --environment "RABBITMQ_RABBITMQCTL_PATH=DOCKER:$$(docker inspect --format='{{.Id}}' $(RABBITMQ_DOCKER_NAME))" \
		--environment 'RABBITMQ_LONG_RUNNING_TESTS=true'
		--environment 'RABBITMQ_TOXIPROXY_TESTS=true' \
		--environment 'PASSWORD=grapefruit' \
		--environment SSL_CERTS_DIR="$(CURDIR)/.ci/certs" \
		"$(CURDIR)/projects/Test/Integration/Integration.csproj" --logger 'console;verbosity=detailed'
	dotnet test --environment "RABBITMQ_RABBITMQCTL_PATH=DOCKER:$$(docker inspect --format='{{.Id}}' $(RABBITMQ_DOCKER_NAME))" $(CURDIR)/projects/Test/SequentialIntegration/SequentialIntegration.csproj --logger 'console;verbosity=detailed'

# Note:
# You must have the expected OAuth2 environment set up for this target
test-all:
	dotnet test --environment "RABBITMQ_RABBITMQCTL_PATH=DOCKER:$$(docker inspect --format='{{.Id}}' $(RABBITMQ_DOCKER_NAME))" $(CURDIR)/Build.csproj --logger 'console;verbosity=detailed'
