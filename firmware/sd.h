#ifndef SD_H
#define SD_H

#include <stdint.h>
uint32_t sd_command(int cmd, uint32_t argument, uint32_t *long_resp);

#endif

