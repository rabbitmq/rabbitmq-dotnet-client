# vim: noexpandtab:ts=4:sw=4
.PHONY: build test test-all

RABBITMQ_DOCKER_NAME ?= rabbitmq-dotnet-client-rabbitmq

build:
	dotnet build $(CURDIR)/Build.csproj

# Note:
#
# --environment 'GITHUB_ACTIONS=true'
#
# The above argument is passed to `dotnet test` because it's assumed you
# use this command to set up your local environment on Linux:
#
# ./.ci/ubuntu/gha-setup.sh toxiproxy
#
# The gha-setup.sh command has been tested on Ubuntu 22 and Arch Linux
# and should work on any Linux system with a recent docker.
#
test:
	dotnet test $(CURDIR)/projects/Test/Unit/Unit.csproj --logger 'console;verbosity=detailed'
	dotnet test --environment 'GITHUB_ACTIONS=true' \
		--environment 'RABBITMQ_LONG_RUNNING_TESTS=true' \
		--environment "RABBITMQ_RABBITMQCTL_PATH=DOCKER:$$(docker inspect --format='{{.Id}}' $(RABBITMQ_DOCKER_NAME))" \
		--environment 'RABBITMQ_TOXIPROXY_TESTS=true' \
		--environment 'PASSWORD=grapefruit' \
		--environment SSL_CERTS_DIR="$(CURDIR)/.ci/certs" \
		"$(CURDIR)/projects/Test/Integration/Integration.csproj" --logger 'console;verbosity=detailed'
	dotnet test --environment 'RABBITMQ_LONG_RUNNING_TESTS=true' \
		--environment "RABBITMQ_RABBITMQCTL_PATH=DOCKER:$$(docker inspect --format='{{.Id}}' $(RABBITMQ_DOCKER_NAME))" \
		--environment 'PASSWORD=grapefruit' \
		--environment SSL_CERTS_DIR="$(CURDIR)/.ci/certs" \
		$(CURDIR)/projects/Test/SequentialIntegration/SequentialIntegration.csproj --logger 'console;verbosity=detailed'

# Note:
# You must have the expected OAuth2 environment set up for this target
test-all:
	dotnet test --environment "RABBITMQ_RABBITMQCTL_PATH=DOCKER:$$(docker inspect --format='{{.Id}}' $(RABBITMQ_DOCKER_NAME))" $(CURDIR)/Build.csproj --logger 'console;verbosity=detailed'
