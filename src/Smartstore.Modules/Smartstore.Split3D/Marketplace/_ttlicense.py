# Injected by the TT Minimal marketplace on every upload. Do not edit: changes are overwritten.
"""Licensing for add-ons sold on the TT Minimal marketplace.

The add-on author ships features only. On upload the shop adds this module, wraps the
add-on's register()/unregister() with :func:`wrap` and obfuscates the package:

* without a valid key the add-on's own classes are not registered; only the
  "TT Minimal" sidebar panel is, where the customer activates a key
  (drag a .ttkey file into the 3D viewport, pick the file, or paste the key);
* the key is activated online for this computer (device limit on the server) and
  re-checked in the background; a key that ends (expired, blocked, signed out)
  unregisters the add-on again;
* new versions are offered by the shop and installed with one click.
"""
import base64
import hashlib
import hmac
import json
import os
import platform
import re
import subprocess
import tempfile
import threading
import time
import uuid
import urllib.error
import urllib.request
from pathlib import Path

import bpy

# Filled in by the shop when it packages the add-on.
PRODUCT = '__TT_PRODUCT__'
ADDON_NAME = '__TT_ADDON_NAME__'
SERVER_URL = '__TT_SERVER_URL__'
UID = '__TT_UID__'  # [a-z0-9_]: makes operator and panel ids unique per add-on

KEY_FILE_EXTENSION = '.ttkey'
KEY_FILE_FORMAT = 'ttminimal-key'
PANEL_CATEGORY = 'TT Minimal'

TIMEOUT = 15
REFRESH_SECONDS = 6 * 3600
STARTUP_REFRESH_SECONDS = 3600
RETRY_SECONDS = 30 * 60
TICK_IDLE = 60.0
TICK_BUSY = 1.0
CLOCK_TOLERANCE = 3600
MAX_TOKEN = 8192
UPDATE_MAX_SIZE = 64 * 1024 * 1024
SHA256_PREFIX = bytes.fromhex('3031300d060960864801650304020105000420')
FATAL_CODES = {'invalid_key', 'unknown_key', 'wrong_product', 'blocked', 'expired', 'deactivated'}
# Fatal answers that the background check still re-checks now and then: the shop may renew or upgrade the key.
RECHECK_CODES = {'expired'}

HERE = Path(__file__).resolve().parent
OP = f'ttlic_{UID}'


def server_url():
    return (os.environ.get('TTMINIMAL_LICENSE_SERVER') or SERVER_URL).rstrip('/')


def shop_url():
    return server_url() + '/cong-cu-3d'


def addon_version():
    try:
        text = (HERE / 'blender_manifest.toml').read_text(encoding='utf-8')
        match = re.search(r'^version\s*=\s*"([^"]+)"', text, re.M)
        return match.group(1) if match else '0.0.0'
    except OSError:
        return '0.0.0'


# ---------------------------------------------------------------- keys (RSA-SHA256, same format as the shop)

def normalize_token(value):
    if not isinstance(value, str) or len(value) > MAX_TOKEN * 2:
        raise ValueError('Key không hợp lệ')
    value = ''.join(c for c in value if not c.isspace() and c not in '﻿​')
    if not value or len(value) > MAX_TOKEN:
        raise ValueError('Key không hợp lệ')
    return value


def _decode(value):
    if not re.fullmatch(r'[A-Za-z0-9_-]+={0,2}', value):
        raise ValueError()
    return base64.b64decode(value + '=' * (-len(value) % 4), altchars=b'-_', validate=True)


def _signed_payload(token):
    payload, signature = normalize_token(token).split('.')
    raw, sig = _decode(payload), _decode(signature)
    public = json.loads((HERE / '_ttlicense_key.json').read_text(encoding='utf-8'))
    n, e = int(public['n']), int(public['e'])
    size = (n.bit_length() + 7) // 8
    if size < 256 or len(sig) != size or int.from_bytes(sig, 'big') >= n:
        raise ValueError()
    digest = SHA256_PREFIX + hashlib.sha256(raw).digest()
    expected = b'\x00\x01' + b'\xff' * (size - len(digest) - 3) + b'\x00' + digest
    if not hmac.compare_digest(pow(int.from_bytes(sig, 'big'), e, n).to_bytes(size, 'big'), expected):
        raise ValueError()
    data = json.loads(raw)
    if not isinstance(data, dict):
        raise ValueError()
    return data


def verify(token, now=None):
    """(valid, message, payload) of a key."""
    try:
        data = _signed_payload(token)
        if data.get('product') != PRODUCT:
            return False, 'Key này dành cho addon khác', {}
        if data.get('v') != 1 or 'kind' in data or not isinstance(data.get('id'), str):
            raise ValueError()
        issued, expiry = data.get('issued'), data.get('expires')
        if type(issued) is not int or (expiry is not None and type(expiry) is not int):
            raise ValueError()
        current = time.time() if now is None else now
        if current < issued - 300:
            return False, 'Đồng hồ máy chưa đúng', data
        if expiry is not None and current >= expiry:
            return False, 'Key đã hết hạn', data
        return True, 'Đã kích hoạt', data
    except (ValueError, TypeError, KeyError, OSError, OverflowError, UnicodeError):
        return False, 'Key không hợp lệ', {}


def verify_lease(lease, key_id, device, now=None):
    if not lease:
        return False, 'Cần kích hoạt online trên máy này', {}
    try:
        data = _signed_payload(lease)
        if data.get('kind') != 'lease' or data.get('product') != PRODUCT or data.get('v') != 1:
            raise ValueError()
        issued, expiry = data.get('issued'), data.get('expires')
        if type(issued) is not int or type(expiry) is not int or not issued < expiry:
            raise ValueError()
    except (ValueError, TypeError, KeyError, OSError, OverflowError, UnicodeError):
        return False, 'Dữ liệu kích hoạt trên máy không hợp lệ; hãy kích hoạt lại', {}
    if data.get('id') != key_id:
        return False, 'Cần kích hoạt online trên máy này', {}
    if data.get('device') != device:
        return False, 'Key đang kích hoạt cho máy khác; hãy kích hoạt lại trên máy này', {}
    current = time.time() if now is None else now
    if current < issued - 300:
        return False, 'Đồng hồ máy chưa đúng', data
    if current >= expiry:
        return False, 'Đã quá hạn dùng offline; hãy kết nối mạng để kiểm tra lại key', data
    return True, 'Đã kích hoạt', data


def token_id(token):
    return hashlib.sha256(token.encode('utf-8')).hexdigest()[:16]


# ---------------------------------------------------------------- storage

def _folder():
    value = bpy.utils.user_resource('CONFIG')
    if not value:
        raise OSError('Không tìm thấy thư mục cấu hình Blender')
    return Path(value) / 'ttminimal' / re.sub(r'[^A-Za-z0-9_.-]+', '_', PRODUCT)


def _write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=path.parent, suffix='.tmp', delete=False) as stream:
            temp = Path(stream.name)
            json.dump(data, stream)
        os.replace(temp, path)
    finally:
        if temp is not None:
            temp.unlink(missing_ok=True)


def _read_json(path):
    try:
        data = json.loads(path.read_text(encoding='utf-8'))
        return data if isinstance(data, dict) else {}
    except (OSError, ValueError):
        return {}


def stored_token():
    try:
        token = _read_json(_folder() / 'key.json').get('token')
    except OSError:
        return None
    return token if isinstance(token, str) else None


def save_token(token):
    _write_json(_folder() / 'key.json', {'product': PRODUCT, 'token': token})
    _CACHE.clear()


def clear_token():
    for name in ('key.json', 'lease.json'):
        try:
            (_folder() / name).unlink(missing_ok=True)
        except OSError:
            pass
    _CACHE.clear()


def read_state():
    try:
        return _read_json(_folder() / 'lease.json')
    except OSError:
        return {}


def write_state(data):
    _write_json(_folder() / 'lease.json', data)
    _CACHE.clear()


# ---------------------------------------------------------------- device

_DEVICE = {}


def _raw_machine_id():
    system = platform.system()
    try:
        if system == 'Windows':
            import winreg
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r'SOFTWARE\Microsoft\Cryptography', 0,
                                winreg.KEY_READ | winreg.KEY_WOW64_64KEY) as key:
                value = winreg.QueryValueEx(key, 'MachineGuid')[0]
                if value:
                    return str(value)
        elif system == 'Darwin':
            out = subprocess.run(['ioreg', '-rd1', '-c', 'IOPlatformExpertDevice'],
                                 capture_output=True, text=True, timeout=5).stdout
            match = re.search(r'"IOPlatformUUID"\s*=\s*"([^"]+)"', out)
            if match:
                return match.group(1)
        for path in ('/etc/machine-id', '/var/lib/dbus/machine-id'):
            try:
                value = Path(path).read_text(encoding='ascii').strip()
                if value:
                    return value
            except OSError:
                continue
    except Exception:
        pass
    return 'node-%012x' % uuid.getnode()


def device_id():
    """Hashed per product: the raw hardware id never leaves the computer."""
    if 'id' not in _DEVICE:
        _DEVICE['id'] = hashlib.sha256(f'{PRODUCT}|{_raw_machine_id()}'.encode('utf-8')).hexdigest()[:40]
    return _DEVICE['id']


def _device_platform():
    system = platform.system()
    if system == 'Darwin':
        return ('macOS ' + platform.mac_ver()[0]).strip()
    return f'{system} {platform.release()}'.strip()


# ---------------------------------------------------------------- status

_CACHE = {}


def status():
    """(licensed, message, key payload). Cached per minute; the files are re-read when they change."""
    try:
        folder = _folder()
        stamp = tuple((p.stat().st_mtime_ns if p.exists() else 0) for p in (folder / 'key.json', folder / 'lease.json'))
    except OSError:
        stamp = None
    cache_key = (stamp, int(time.time() // 60))
    if _CACHE.get('key') == cache_key:
        return _CACHE['value']
    value = _read_status()
    _CACHE['key'], _CACHE['value'] = cache_key, value
    return value


def _read_status():
    token = stored_token()
    if not token:
        return False, 'Chưa kích hoạt', {}
    valid, message, key = verify(token)
    if not valid:
        return valid, message, key
    state = read_state()
    if state.get('token_id') != token_id(token):
        return False, ('Đang kiểm tra key với máy chủ…' if _JOB['thread'] else 'Cần kích hoạt online trên máy này'), key
    if state.get('code') != 'ok':
        return False, state.get('message') or 'Cần kích hoạt online trên máy này', key
    seen = state.get('seen')
    if isinstance(seen, (int, float)) and time.time() < seen - CLOCK_TOLERANCE:
        return False, 'Đồng hồ máy bị chỉnh lùi; hãy chỉnh lại ngày giờ', key
    ok, lease_message, lease = verify_lease(state.get('lease'), key['id'], device_id())
    if not ok:
        return False, lease_message, key
    return True, message, dict(key, _lease=lease)


# ---------------------------------------------------------------- server

class ServerUnavailable(Exception):
    pass


def online_allowed():
    if getattr(bpy.app, 'online_access', True):
        return True
    try:
        return bool(bpy.context.preferences.system.use_online_access)
    except AttributeError:
        return False


def _ssl_context():
    try:
        import ssl
        return ssl.create_default_context()
    except Exception:
        return None


def _body(token):
    return json.dumps({
        'token': token,
        'product': PRODUCT,
        'device_id': device_id(),
        'device_name': (platform.node() or 'Computer')[:200],
        'platform': _device_platform(),
        'blender': bpy.app.version_string,
        'addon_version': addon_version(),
    }).encode('utf-8')


def _post(action, body):
    request = urllib.request.Request(
        f'{server_url()}/split3d/api/{action}', data=body, method='POST',
        headers={'Content-Type': 'application/json', 'User-Agent': f'TTMinimal/{addon_version()}'})
    try:
        with urllib.request.urlopen(request, timeout=TIMEOUT, context=_ssl_context()) as response:
            data = json.loads(response.read(65536).decode('utf-8'))
    except (urllib.error.URLError, OSError, ValueError) as error:
        raise ServerUnavailable(str(getattr(error, 'reason', error))) from error
    if not isinstance(data, dict) or 'code' not in data:
        raise ServerUnavailable('Máy chủ trả lời không hợp lệ')
    return data


def message_for(result):
    code = result.get('code')
    if code == 'device_limit':
        names = ', '.join(result.get('devices') or [])
        return f"Key đã dùng đủ {result.get('max_devices', '?')} máy ({names}). Đăng xuất máy cũ tại trang Key của tôi."
    return {
        'invalid_key': 'Key không hợp lệ',
        'unknown_key': 'Key chưa được đăng ký; hãy liên hệ shop',
        'wrong_product': 'Key này dành cho addon khác',
        'blocked': 'Key đã bị khoá; hãy liên hệ shop',
        'expired': 'Key đã hết hạn',
        'deactivated': 'Máy này đã bị đăng xuất khỏi key; bấm Kích hoạt lại',
        'server_error': 'Máy chủ đang lỗi; hãy thử lại sau',
    }.get(code) or result.get('message') or str(code)


def _version_tuple(value):
    try:
        return tuple(int(part) for part in str(value).split('.'))
    except ValueError:
        return ()


def _valid_update(info):
    if not isinstance(info, dict):
        return None
    version, url, digest, size = info.get('version'), info.get('url'), info.get('hash'), info.get('size')
    if not (isinstance(version, str) and isinstance(url, str) and isinstance(digest, str)
            and type(size) is int and 0 < size <= UPDATE_MAX_SIZE):
        return None
    if not url.startswith(server_url() + '/split3d/repo/') or not url.endswith('.zip'):
        return None
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', digest) or _version_tuple(version) <= _version_tuple(addon_version()):
        return None
    return {'version': version, 'url': url, 'hash': digest, 'size': size}


def _adopt_token(result, token):
    """After a plan upgrade the shop re-signs the key (same key id, new expiry) and sends it along with the
    answer. Returns the key to keep: the new one when it is genuine, valid and the same key, else `token`."""
    new = result.get('token')
    if not isinstance(new, str) or new == token:
        return token
    try:
        new = normalize_token(new)
    except ValueError:
        return token
    old = verify(token)[2]
    valid, _, data = verify(new)
    if not valid or not old or data.get('id') != old.get('id'):
        return token
    if stored_token() in (None, token):
        save_token(new)
    return new


def _lease_devices(lease):
    if not lease:
        return None
    try:
        value = _signed_payload(lease).get('max_devices')
    except Exception:
        return None
    return value if type(value) is int else None


def _plan_change(previous, old_token, token, lease):
    """What the shop changed in the plan of the SAME key (expiry and/or device limit), or None."""
    old_key, new_key = verify(old_token)[2], verify(token)[2]
    if not old_key or not new_key or old_key.get('id') != new_key.get('id'):
        return None
    same_key = previous.get('token_id') in (token_id(old_token), token_id(token))
    old_devices = _lease_devices(previous.get('lease')) if same_key else None
    new_devices = _lease_devices(lease)
    expires_changed = old_key.get('expires') != new_key.get('expires')
    devices_changed = old_devices is not None and new_devices is not None and old_devices != new_devices
    if not expires_changed and not devices_changed:
        return None
    return {'old_expires': old_key.get('expires'), 'new_expires': new_key.get('expires'),
            'old_devices': old_devices, 'new_devices': new_devices, 'at': int(time.time())}


def _store_result(result, token):
    now = int(time.time())
    if result.get('ok') and result.get('lease'):
        previous = read_state()
        old_token = token
        token = _adopt_token(result, token)
        # A plan change is announced once by a popup (see _show_popups); until shown it survives later checks.
        notice = _plan_change(previous, old_token, token, result['lease'])
        if notice is None and previous.get('token_id') in (token_id(old_token), token_id(token)):
            notice = previous.get('notice')
        write_state({'lease': result['lease'], 'token_id': token_id(token), 'checked': now, 'seen': now,
                     'code': 'ok', 'update': _valid_update(result.get('update')), 'notice': notice})
        return True
    if result.get('code') in FATAL_CODES:
        write_state({'lease': None, 'token_id': token_id(token), 'checked': now, 'seen': now,
                     'code': result.get('code'), 'message': message_for(result)})
    return False


def available_update():
    return _valid_update(read_state().get('update'))


def activate(token):
    """Blocking activation on this computer. Returns (ok, message)."""
    if not online_allowed():
        raise ServerUnavailable('Blender đang tắt truy cập mạng (Preferences > System > Network > Allow Online Access)')
    result = _post('activate', _body(token))
    if result.get('ok') and result.get('lease'):
        save_token(token)
        _store_result(result, token)  # may replace the key with an upgraded one
        return True, 'Kích hoạt thành công'
    return False, message_for(result)


# ---------------------------------------------------------------- background check

# 'note' is the outcome of the last check the customer started by hand, shown in the panel.
_JOB = {'thread': None, 'done': False, 'last_attempt': 0.0, 'session_checked': False, 'error': None, 'note': None}


def _worker(token, body, first):
    try:
        result = _post('activate' if first else 'refresh', body)
        if _store_result(result, token):
            _JOB['error'] = None
        else:
            _JOB['error'] = message_for(result)
    except Exception as error:  # never let a background check crash Blender
        _JOB['error'] = str(error)
    finally:
        _JOB['done'] = True


def _job_finish():
    """Clears a finished background check. Returns True while a check is still running."""
    if _JOB['thread'] is not None and _JOB['done']:
        _JOB['thread'] = None
        _CACHE.clear()
        _redraw()
    return _JOB['thread'] is not None


def _poll_manual_check():
    """Fast timer while a check started from the panel runs, so the answer shows within a second."""
    if _job_finish():
        return 0.5
    if _JOB['error']:
        _JOB['note'] = ('ERROR', 'Không kiểm tra được: ' + _JOB['error'])
    elif available_update():
        _JOB['note'] = None
    else:
        _JOB['note'] = ('CHECKMARK', f'Đang dùng bản mới nhất ({addon_version()})')
    _redraw()
    return None


def refresh_now(force=False):
    _job_finish()
    if _JOB['thread'] is not None or not online_allowed():
        return False
    token = stored_token()
    if not token:
        return False
    state = read_state()
    if state.get('token_id') != token_id(token):
        state = {}
    if state.get('code') in FATAL_CODES - RECHECK_CODES:
        return False
    if not force:
        age = time.time() - state.get('checked', 0)
        due = not state or age >= REFRESH_SECONDS or (not _JOB['session_checked'] and age >= STARTUP_REFRESH_SECONDS)
        if not due or time.time() - _JOB['last_attempt'] < RETRY_SECONDS:
            return False
    _JOB.update(last_attempt=time.time(), session_checked=True, done=False)
    thread = threading.Thread(target=_worker, args=(token, _body(token), not state), daemon=True, name=f'{OP}-check')
    _JOB['thread'] = thread
    thread.start()
    return True


def _redraw():
    try:
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:
                area.tag_redraw()
    except Exception:
        pass


def _tick():
    try:
        if not _job_finish():
            refresh_now()
        _sync_core()
        if _JOB['thread'] is None:
            _show_popups()
    except Exception as error:
        print(f'{ADDON_NAME} license check:', error)
    return TICK_BUSY if _JOB['thread'] is not None else TICK_IDLE


# ---------------------------------------------------------------- the add-on's own register / unregister

_CORE = {'register': None, 'unregister': None, 'on': False, 'error': None}


def _sync_core():
    """Registers the add-on while the key is valid and unregisters it when the key ends."""
    licensed = status()[0]
    if licensed and not _CORE['on']:
        try:
            _CORE['register']()
            _CORE['on'] = True
            _CORE['error'] = None
        except Exception as error:
            _CORE['error'] = str(error)
            print(f'{ADDON_NAME}: register failed:', error)
            try:
                _CORE['unregister']()
            except Exception:
                pass
    elif not licensed and _CORE['on']:
        try:
            _CORE['unregister']()
        except Exception as error:
            print(f'{ADDON_NAME}: unregister failed:', error)
        _CORE['on'] = False
    _redraw()


def wrap(register, unregister):
    """Returns the register()/unregister() pair Blender calls instead of the add-on's own."""
    _CORE['register'], _CORE['unregister'] = register, unregister

    def wrapped_register():
        _register_ui()
        _sync_core()
        if not bpy.app.timers.is_registered(_tick):
            bpy.app.timers.register(_tick, first_interval=3.0, persistent=True)

    def wrapped_unregister():
        if bpy.app.timers.is_registered(_tick):
            bpy.app.timers.unregister(_tick)
        if _CORE['on']:
            try:
                _CORE['unregister']()
            finally:
                _CORE['on'] = False
        _unregister_ui()

    return wrapped_register, wrapped_unregister


# ---------------------------------------------------------------- UI: panel, operators, key file drop

def _read_key_file(path):
    """The token inside a .ttkey file (JSON written by the shop) or a plain text key."""
    text = Path(path).read_text(encoding='utf-8-sig').strip()
    if text.startswith('{'):
        data = json.loads(text)
        if data.get('format') != KEY_FILE_FORMAT:
            raise ValueError('Không phải file key của TT Minimal')
        if data.get('product') and data['product'] != PRODUCT:
            raise ValueError(f"File key này dành cho {data.get('addon') or 'addon khác'}")
        return data.get('token') or ''
    return text


def _activate(operator, token):
    try:
        token = normalize_token(token)
    except ValueError as error:
        operator.report({'ERROR'}, str(error))
        return {'CANCELLED'}
    valid, message, data = verify(token)
    expired = bool(data) and isinstance(data.get('expires'), int) and time.time() >= data['expires']
    if not valid and not expired:
        operator.report({'ERROR'}, message)
        return {'CANCELLED'}
    try:
        ok, message = activate(token)
    except ServerUnavailable as error:
        operator.report({'ERROR'}, 'Không kết nối được máy chủ: ' + str(error))
        return {'CANCELLED'}
    if not ok:
        operator.report({'ERROR'}, message)
        return {'CANCELLED'}
    _sync_core()
    operator.report({'INFO'}, f'{ADDON_NAME}: {message}')
    return {'FINISHED'}


def _op_import_key_execute(self, context):
    # A drop sets directory + files (like Blender's own importers); the file browser sets filepath.
    paths = [os.path.join(self.directory, f.name) for f in self.files if f.name] if self.files else []
    if not paths and self.filepath:
        paths = [self.filepath]
    paths = [p for p in paths if p.lower().endswith(KEY_FILE_EXTENSION)] or paths
    if not paths:
        self.report({'ERROR'}, 'Chưa chọn file key (.ttkey)')
        return {'CANCELLED'}
    try:
        token = _read_key_file(paths[0])
    except (OSError, ValueError) as error:
        self.report({'ERROR'}, str(error))
        return {'CANCELLED'}
    return _activate(self, token)


def _op_import_key_invoke(self, context, event):
    props = self.properties
    if props.is_property_set('filepath') or props.is_property_set('files'):  # dropped into Blender
        return self.execute(context)
    context.window_manager.fileselect_add(self)
    return {'RUNNING_MODAL'}


def _op_paste_key_execute(self, context):
    return _activate(self, context.window_manager.clipboard or '')


def _op_check_execute(self, context):
    if not online_allowed():
        self.report({'ERROR'}, 'Hãy bật Preferences > System > Network > Allow Online Access')
        return {'CANCELLED'}
    state = read_state()
    if state.get('code') in FATAL_CODES:
        # A fatal answer is final for the background check; a manual check activates again.
        token = stored_token()
        return _activate(self, token) if token else {'CANCELLED'}
    if not refresh_now(force=True) and _JOB['thread'] is None:
        self.report({'ERROR'}, 'Chưa kiểm tra được; hãy thử lại')
        return {'CANCELLED'}
    _JOB['note'] = None
    if not bpy.app.timers.is_registered(_poll_manual_check):
        bpy.app.timers.register(_poll_manual_check, first_interval=0.5)
    self.report({'INFO'}, 'Đang kiểm tra key và bản cập nhật…')
    return {'FINISHED'}


def _op_update_execute(self, context):
    info = available_update()
    if not info:
        self.report({'INFO'}, 'Đang dùng bản mới nhất')
        return {'CANCELLED'}
    parts = (__package__ or '').split('.')
    if len(parts) != 3 or parts[0] != 'bl_ext':
        self.report({'ERROR'}, 'Hãy tải bản mới ở trang Key của tôi và kéo file zip vào Blender')
        return {'CANCELLED'}
    try:
        request = urllib.request.Request(info['url'], headers={'User-Agent': f'TTMinimal/{addon_version()}'})
        with urllib.request.urlopen(request, timeout=60, context=_ssl_context()) as response:
            data = response.read(UPDATE_MAX_SIZE + 1)
    except (urllib.error.URLError, OSError) as error:
        self.report({'ERROR'}, 'Không tải được bản cập nhật: ' + str(getattr(error, 'reason', error)))
        return {'CANCELLED'}
    if len(data) != info['size'] or 'sha256:' + hashlib.sha256(data).hexdigest() != info['hash']:
        self.report({'ERROR'}, 'Tệp cập nhật bị lỗi; hãy thử lại')
        return {'CANCELLED'}
    path = Path(tempfile.mkdtemp(prefix='ttminimal-update-')) / info['url'].rsplit('/', 1)[1]
    path.write_bytes(data)
    # Installing replaces this add-on, including this operator: doing it inside execute() frees the running
    # operator and crashes Blender on the next access. Install right after the operator has returned.
    bpy.app.timers.register(lambda: _install_update(str(path), parts[1], info['version']), first_interval=0.2)
    self.report({'INFO'}, f"Đang cài {ADDON_NAME} bản {info['version']}…")
    return {'FINISHED'}


def _install_update(path, repo, version):
    """Timer callback: installs the downloaded package over this add-on. Must not touch operator
    or panel instances of this module, they are unregistered by the install."""
    window = None
    try:
        wm = bpy.context.window_manager
        window = wm.windows[0] if wm and wm.windows else None
        if window is not None:
            with bpy.context.temp_override(window=window):
                result = bpy.ops.extensions.package_install_files(filepath=path, repo=repo, enable_on_install=True)
        else:
            result = bpy.ops.extensions.package_install_files(filepath=path, repo=repo, enable_on_install=True)
        # Blender's installer answers FINISHED even when it could not replace the files (e.g. a file of the
        # add-on is locked on Windows); the version on disk tells whether the update really happened.
        installed = addon_version()
        if 'FINISHED' in result and _version_tuple(installed) >= _version_tuple(version):
            message = f'Đã cập nhật {ADDON_NAME} lên bản {installed}'
        else:
            message = (f'Không cài được bản {version} (vẫn là {installed}). Hãy khởi động lại Blender rồi bấm Cập nhật lại, '
                       'hoặc tải file zip ở trang Key của tôi và kéo vào Blender.')
    except Exception as error:
        message = f'Không cài được bản cập nhật {version}: {error}'
    _JOB['note'] = ('CHECKMARK' if message.startswith('Đã') else 'ERROR', message)
    print(message)
    # A popup needs a window; in background mode (or before a window exists) only the console message remains.
    if window is not None and not bpy.app.background:
        try:
            def draw(menu, _context):
                menu.layout.label(text=message)
            with bpy.context.temp_override(window=window):
                bpy.context.window_manager.popup_menu(draw, title=ADDON_NAME, icon='INFO')
        except Exception:
            pass
    return None  # run once


# ---------------------------------------------------------------- popups

# A new version is announced once per Blender session (so at every start until it is installed);
# a plan change (upgrade/downgrade by the shop) only once. The panel always shows the current state.
_POPUP = {'update_shown': None, 'kind': None, 'data': None, 'opened': 0.0}
POPUP_MAX_SECONDS = 600  # a dialog left open longer no longer blocks the next one


def _popup_target():
    if bpy.app.background:
        return None
    wm = getattr(bpy.context, 'window_manager', None)
    if not wm or not wm.windows or wm.windows[0].screen is None:
        return None
    window = wm.windows[0]
    for area in sorted(window.screen.areas, key=lambda a: (a.type != 'VIEW_3D', -a.width * a.height)):
        region = next((r for r in area.regions if r.type == 'WINDOW'), None)
        if region is not None:
            return window, area, region
    return None


def _open_popup(kind, data):
    target = _popup_target()
    if target is None:
        return False
    _POPUP['kind'], _POPUP['data'] = kind, data
    _POPUP['opened'] = time.time()
    window, area, region = target
    try:
        with bpy.context.temp_override(window=window, area=area, region=region, screen=window.screen):
            operator = getattr(bpy.ops, OP)
            result = operator.notice('INVOKE_DEFAULT')
    except Exception as error:
        print(f'{ADDON_NAME} popup:', error)
        return False
    return bool(result & {'RUNNING_MODAL', 'FINISHED'})


def _show_popups():
    if time.time() - _POPUP['opened'] < POPUP_MAX_SECONDS:
        return  # one dialog at a time: the next one waits until this one is closed
    notice = read_state().get('notice')
    if isinstance(notice, dict):
        if _open_popup('plan', notice):
            state = read_state()
            state.pop('notice', None)
            write_state(state)
        return
    info = available_update()
    if info and _POPUP['update_shown'] != info['version'] and status()[0]:
        if _open_popup('update', info):
            _POPUP['update_shown'] = info['version']


def _format_expiry(value):
    return time.strftime('%d/%m/%Y', time.localtime(value)) if value else 'Vĩnh viễn'


def _plan_title(data):
    old, new = data.get('old_expires'), data.get('new_expires')
    longer = (old and not new) or (old and new and new > old)
    more = data.get('old_devices') is not None and (data.get('new_devices') or 0) > data['old_devices']
    shorter = (new and not old) or (old and new and new < old)
    return 'Key đã được nâng cấp' if (longer or more) and not shorter else 'Gói key đã thay đổi'


def _op_notice_invoke(self, context, event):
    data = _POPUP['data'] or {}
    if _POPUP['kind'] == 'update':
        title, confirm = f'Có bản cập nhật {ADDON_NAME}', 'Cập nhật ngay'
    else:
        title, confirm = _plan_title(data), 'OK'
    try:
        return context.window_manager.invoke_props_dialog(self, width=460, title=title, confirm_text=confirm)
    except TypeError:  # Blender < 4.1 has no title/confirm_text
        return context.window_manager.invoke_props_dialog(self, width=460)


def _op_notice_draw(self, context):
    data = _POPUP['data'] or {}
    col = self.layout.column(align=True)
    if _POPUP['kind'] == 'update':
        col.label(text=f"Có bản mới {data.get('version', '?')} (đang dùng {addon_version()})", icon='IMPORT')
        col.separator()
        col.label(text='Bấm Cập nhật ngay để tải và cài đè bản đang dùng (không cần gỡ addon).')
        col.label(text='Thông báo này hiện mỗi lần mở Blender cho tới khi bạn cập nhật.')
        return
    col.label(text=ADDON_NAME, icon='CHECKMARK')
    col.separator()
    if data.get('old_expires') != data.get('new_expires'):
        col.label(text=f"Hạn sử dụng: {_format_expiry(data.get('old_expires'))}  →  {_format_expiry(data.get('new_expires'))}", icon='TIME')
    if data.get('old_devices') is not None and data.get('new_devices') is not None and data['old_devices'] != data['new_devices']:
        col.label(text=f"Số máy: {data['old_devices']}  →  {data['new_devices']}", icon='DESKTOP')
    col.separator()
    col.label(text='Thông tin trong bảng TT Minimal đã được cập nhật.')
    col.label(text='Không cần cài lại addon hay nhập key mới.')


def _op_notice_cancel(self, context):
    _POPUP['opened'] = 0.0


def _op_notice_execute(self, context):
    _POPUP['opened'] = 0.0
    if _POPUP['kind'] == 'update' and available_update():
        return _op_update_execute(self, context)
    return {'FINISHED'}


def _op_sign_out_execute(self, context):
    token = stored_token()
    if token and online_allowed():
        try:
            _post('deactivate', _body(token))
        except ServerUnavailable:
            pass
    clear_token()
    _sync_core()
    self.report({'INFO'}, 'Đã đăng xuất key khỏi máy này')
    return {'FINISHED'}


def _op_shop_execute(self, context):
    bpy.ops.wm.url_open(url=shop_url())
    return {'FINISHED'}


def _op_online_execute(self, context):
    try:
        context.preferences.system.use_online_access = True
    except AttributeError:
        return {'CANCELLED'}
    refresh_now(force=True)
    return {'FINISHED'}


def _wrap(text, width):
    import textwrap
    return textwrap.wrap(text, width) or ['']


def _panel_draw(self, context):
    layout = self.layout
    ok, message, key = status()

    if ok:
        row = layout.row()
        row.label(text='Đã kích hoạt', icon='CHECKMARK')
        expires = key.get('expires')
        layout.label(text='Hạn: ' + (time.strftime('%d/%m/%Y', time.localtime(expires)) if expires else 'Vĩnh viễn'), icon='TIME')
        if _CORE['error']:
            layout.label(text='Lỗi khi bật addon: ' + _CORE['error'][:80], icon='ERROR')
        info = available_update()
        if info:
            box = layout.box()
            box.label(text=f"Có bản mới {info['version']} (đang dùng {addon_version()})", icon='IMPORT')
            box.operator(f'{OP}.update', text=f"Cập nhật lên bản {info['version']}", icon='FILE_REFRESH')
        else:
            layout.label(text=f'Phiên bản {addon_version()}', icon='INFO')
        if _JOB['thread'] is not None:
            layout.label(text='Đang kiểm tra với máy chủ…', icon='SORTTIME')
        elif _JOB['note']:
            icon, text = _JOB['note']
            col = layout.column(align=True)
            col.alert = icon == 'ERROR'
            # Long messages wrap over several labels.
            for i, line in enumerate(_wrap(text, 42)):
                col.label(text=line, icon=icon if i == 0 else 'BLANK1')
        row = layout.row(align=True)
        row.enabled = _JOB['thread'] is None
        row.operator(f'{OP}.check', text='Kiểm tra cập nhật', icon='FILE_REFRESH')
        row.operator(f'{OP}.sign_out', text='', icon='QUIT')
        return

    col = layout.column()
    col.alert = message not in ('Chưa kích hoạt',)
    col.label(text=message, icon='LOCKED')
    if not online_allowed():
        layout.operator(f'{OP}.online', text='Cho phép Blender truy cập mạng', icon='WORLD')
    box = layout.box()
    box.label(text='Kéo file key (.ttkey) thả vào Blender', icon='FILE_TICK')
    box.operator(f'{OP}.import_key', text='Chọn file key…', icon='FILEBROWSER')
    box.operator(f'{OP}.paste_key', text='Dán key', icon='PASTEDOWN')
    if stored_token():
        layout.operator(f'{OP}.check', text='Kích hoạt lại', icon='FILE_REFRESH')
    layout.operator(f'{OP}.shop', text='Mua key', icon='URL')


_UI = []


def _build_ui():
    ops = [
        type(f'TTLIC_OT_{UID}_import_key', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.import_key', 'bl_label': 'Kích hoạt bằng file key',
            'bl_description': 'Chọn file .ttkey tải từ trang Key của tôi (hoặc kéo thả file vào cửa sổ 3D)',
            'bl_options': {'REGISTER'},
            '__annotations__': {
                'filepath': bpy.props.StringProperty(subtype='FILE_PATH', options={'SKIP_SAVE'}),
                'directory': bpy.props.StringProperty(subtype='DIR_PATH', options={'SKIP_SAVE', 'HIDDEN'}),
                'files': bpy.props.CollectionProperty(type=bpy.types.OperatorFileListElement, options={'SKIP_SAVE', 'HIDDEN'}),
                'filter_glob': bpy.props.StringProperty(default='*' + KEY_FILE_EXTENSION, options={'HIDDEN'}),
            },
            'execute': _op_import_key_execute, 'invoke': _op_import_key_invoke}),
        type(f'TTLIC_OT_{UID}_paste_key', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.paste_key', 'bl_label': 'Dán key', 'bl_description': 'Kích hoạt bằng key đã copy',
            'bl_options': {'INTERNAL'}, 'execute': _op_paste_key_execute}),
        type(f'TTLIC_OT_{UID}_check', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.check', 'bl_label': 'Kiểm tra key và cập nhật',
            'bl_description': 'Kiểm tra key với máy chủ và xem có bản mới không', 'bl_options': {'INTERNAL'},
            'execute': _op_check_execute}),
        type(f'TTLIC_OT_{UID}_update', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.update', 'bl_label': 'Cập nhật addon',
            'bl_description': 'Tải bản mới từ shop và cài đè bản đang dùng', 'bl_options': {'INTERNAL'},
            'execute': _op_update_execute}),
        type(f'TTLIC_OT_{UID}_notice', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.notice', 'bl_label': ADDON_NAME, 'bl_options': {'INTERNAL'},
            'invoke': _op_notice_invoke, 'draw': _op_notice_draw, 'execute': _op_notice_execute,
            'cancel': _op_notice_cancel}),
        type(f'TTLIC_OT_{UID}_sign_out', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.sign_out', 'bl_label': 'Đăng xuất key',
            'bl_description': 'Gỡ key khỏi máy này để dùng trên máy khác', 'bl_options': {'INTERNAL'},
            'execute': _op_sign_out_execute}),
        type(f'TTLIC_OT_{UID}_shop', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.shop', 'bl_label': 'Mua key', 'bl_options': {'INTERNAL'},
            'execute': _op_shop_execute}),
        type(f'TTLIC_OT_{UID}_online', (bpy.types.Operator,), {
            'bl_idname': f'{OP}.online', 'bl_label': 'Cho phép truy cập mạng', 'bl_options': {'INTERNAL'},
            'execute': _op_online_execute}),
        type(f'TTLIC_PT_{UID}', (bpy.types.Panel,), {
            'bl_idname': f'TTLIC_PT_{UID}', 'bl_label': ADDON_NAME, 'bl_space_type': 'VIEW_3D',
            'bl_region_type': 'UI', 'bl_category': PANEL_CATEGORY, 'draw': _panel_draw}),
    ]
    if hasattr(bpy.types, 'FileHandler'):
        ops.append(type(f'TTLIC_FH_{UID}', (bpy.types.FileHandler,), {
            'bl_idname': f'TTLIC_FH_{UID}', 'bl_label': f'{ADDON_NAME} key',
            'bl_import_operator': f'{OP}.import_key', 'bl_file_extensions': KEY_FILE_EXTENSION,
            # Any editor accepts the key file, not only the 3D viewport.
            'poll_drop': classmethod(lambda cls, context: context.area is not None)}))
    return ops


def _register_ui():
    if _UI:
        return
    for cls in _build_ui():
        bpy.utils.register_class(cls)
        _UI.append(cls)


def _unregister_ui():
    for cls in reversed(_UI):
        try:
            bpy.utils.unregister_class(cls)
        except RuntimeError:
            pass
    _UI.clear()
