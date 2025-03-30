
FROM ruby:3.2.7

ARG OSS_EMULATOR_VERSION=1.0.0
ARG OSSUTIL_VERSION=1.7.19

RUN git clone --branch ${OSS_EMULATOR_VERSION} --depth 1 https://github.com/aliyun/oss-emulator
RUN gem install thor builder webrick

RUN wget https://gosspublic.alicdn.com/ossutil/${OSSUTIL_VERSION}/ossutil-v${OSSUTIL_VERSION}-linux-amd64.zip -O ossutil.zip
RUN unzip ossutil.zip
RUN /ossutil-v${OSSUTIL_VERSION}-linux-amd64/ossutil64 config -e http://127.0.0.1:8280 --access-key-id AccessKeyId --access-key-secret AccessKeySecret --config-file /root/.ossutilconfig
ENV PATH=${PATH}:/ossutil-v${OSSUTIL_VERSION}-linux-amd64

WORKDIR /oss-emulator
ENTRYPOINT [ "ruby", "/oss-emulator/bin/emulator", "-r", "store", "-p", "8280" ]
# ENTRYPOINT ruby /oss-emulator/bin/emulator -r /oss-emulator/store -p 8280
