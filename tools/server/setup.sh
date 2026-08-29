#!/usr/bin/env bash
#
# Первичная настройка сервера под сайт игры и раздачу обновлений.
#
# Скрипт, а не набор команд по памяти: сервер однажды придётся переставить
# или перенести к другому хостеру, и тогда всё это повторяется одной строкой,
# а не восстанавливается по переписке.
#
# Запускается сколько угодно раз подряд: каждый шаг проверяет, не сделан ли
# он уже. Это важнее, чем кажется — половина работы с сервером состоит из
# «а я это уже делал или нет».
#
# Использование:
#   scp setup.sh root@сервер:/root/ && ssh root@сервер bash /root/setup.sh

set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

SITE_ROOT=/var/www/game
NGINX_SITE=/etc/nginx/sites-available/game

echo "== Система"
. /etc/os-release
echo "   $PRETTY_NAME"

# --- Пакеты ---------------------------------------------------------------

echo "== Обновляю списки пакетов"
apt-get update -qq

echo "== Ставлю веб-сервер и файрвол"
apt-get install -y -qq nginx ufw >/dev/null

# --- Вход только по ключу --------------------------------------------------
#
# Пароль root, ушедший в переписку, после этого шага не открывает ничего.
# Отдельный файл в sshd_config.d, а не правка основного конфига: обновление
# системы перезапишет основной, а этот оставит.
#
# Имя начинается с 00 не для красоты. В sshd побеждает ПЕРВОЕ найденное
# значение, а не последнее, — правило, обратное почти всем остальным
# конфигам. Хостеры кладут сюда свой 50-cloud-init.conf с разрешённым
# паролем, и файл с именем 99- читается после него и не значит ничего.
# Причём тихо: строка в конфиге есть, а действия у неё нет.

echo "== Выключаю вход по паролю"

# Убираем прошлое имя, если оно осталось от старой версии скрипта.
rm -f /etc/ssh/sshd_config.d/99-oxbox.conf

cat > /etc/ssh/sshd_config.d/00-oxbox.conf <<'CONF'
# Ставится скриптом настройки. Правки руками пропадут при следующем прогоне.
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin prohibit-password
CONF

# Проверяем конфиг ДО перезапуска. Сломанный конфиг + reload означает потерю
# доступа к серверу, и чинить это можно будет только через консоль хостера.
if sshd -t 2>/dev/null || /usr/sbin/sshd -t; then
  systemctl reload ssh 2>/dev/null || systemctl reload sshd
else
  echo "   ОШИБКА в конфиге SSH, откатываю"
  rm -f /etc/ssh/sshd_config.d/00-oxbox.conf
  exit 1
fi

# Проверяем не то, что файл записан, а то, что sshd с ним согласился.
# Записанная строка и применённая настройка — разные вещи, и разошлись
# они здесь именно молча.
#
# grep без -q намеренно. С -q он выходит на первом же совпадении и
# закрывает трубу, sshd умирает от SIGPIPE с кодом 141, а pipefail
# объявляет весь конвейер неуспешным — то есть проверка проваливается
# ровно тогда, когда находит искомое. Без -q grep дочитывает поток.
if sshd -T 2>/dev/null | grep -i "^passwordauthentication no" >/dev/null; then
  echo "   готово, теперь только по ключу"
else
  echo "   ВНИМАНИЕ: пароль всё ещё разрешён. Смотри, кто задаёт его раньше:"
  grep -rn -i "passwordauthentication" /etc/ssh/sshd_config.d/ || true
  exit 1
fi

# --- Файрвол ---------------------------------------------------------------

echo "== Настраиваю файрвол"

# SSH первым и до включения: иначе ufw закроет порт вместе со мной внутри.
ufw allow OpenSSH >/dev/null
ufw allow 'Nginx Full' >/dev/null
ufw --force enable >/dev/null

echo "   открыты: SSH, HTTP, HTTPS"

# --- Место под сайт --------------------------------------------------------

echo "== Готовлю папки"

mkdir -p "$SITE_ROOT"
mkdir -p "$SITE_ROOT/downloads"

# --- Веб-сервер ------------------------------------------------------------

echo "== Настраиваю nginx"

cat > "$NGINX_SITE" <<'CONF'
server {
    listen 80 default_server;
    listen [::]:80 default_server;

    root /var/www/game;
    index index.html;

    # Кириллица в именах файлов установщика.
    charset utf-8;

    location / {
        try_files $uri $uri/ =404;
    }

    # Файл истории версий лаунчер спрашивает при каждом запуске, и ему нужна
    # свежая копия, а не та, что осела в кеше промежуточного узла.
    location ~* \.(md|json)$ {
        add_header Cache-Control "no-cache, must-revalidate";
    }

    # Сборки игры — большие файлы. Отдаём как загрузку и разрешаем докачку:
    # шестьдесят мегабайт по плохому каналу с первого раза доезжают не всегда.
    location /downloads/ {
        add_header Content-Disposition "attachment";
        add_header Accept-Ranges bytes;
    }

    # Сжатие текста. Страница истории — 450 КБ, из них почти всё логотип,
    # но разметка и стили ужимаются вчетверо.
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/html;
    gzip_min_length 1024;
}
CONF

ln -sf "$NGINX_SITE" /etc/nginx/sites-enabled/game
rm -f /etc/nginx/sites-enabled/default

if nginx -t 2>/dev/null; then
  systemctl reload nginx
  echo "   готово"
else
  echo "   ОШИБКА в конфиге nginx:"
  nginx -t
  exit 1
fi

# --- Итог ------------------------------------------------------------------

systemctl is-active --quiet nginx && echo "== nginx работает"
echo "== Корень сайта: $SITE_ROOT"
echo "== Сборки класть в: $SITE_ROOT/downloads"
